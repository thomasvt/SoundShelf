using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using SoundShelf.Library;
using SoundShelf.ShortLists;
using File = System.IO.File;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace SoundShelf
{
    public record ShortListMembership(SearchableSound Sound, ShortList ShortList, bool IsMember);

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
    {
        private List<SearchableSound>? _searchableSoundsCache;
        private readonly SoundPlayer _soundPlayer = new();
        private int _scanTaskTotal;
        private int _scanTaskDone;
        private readonly SoundLibrary _library = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundShelf", "index.json"));
        private readonly string _configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundShelf", "config.json");
        private string _searchQuery = "";
        private string _countLabel;
        private SoundFile? _currentSoundFile;
        private TimeSpan _cursor;
        private TimeSpan _stopAtTime;
        private string _scanProgressMessage;
        private Visibility _progressVisibility = Visibility.Hidden;
        private Configuration _configuration;
        private string _libraryRootPaths;
        private ResultVisualizationMode _resultVisualizationMode;
        private string _tagIgnoreList;
        private string _newShortListName;
        private ShortList? CurrentShortList;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering += OnRendering;

            LoadConfiguration();
            LoadSoundLibrary();
            LoadShortLists();
            ScanProgressMessage = $"Your SoundShelf contains {_library.Sounds.Count} sound(s).";
            ReprocessSearch();
        }

        private void LoadSoundLibrary()
        {
            try
            {
                _library.Load();
                InvalidateSearchableSoundsCache();
            }
            catch (Exception e)
            {
                MessageBox.Show(
                    "Could not load library from index file. It is probably corrupt. If you manually changed the index file, fix it; or Clean the library from the Manage tab in SoundShelf.\n\nError:\n\n" + e.Message,
                    "Library corrupt", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadShortLists()
        {
            var dir = GetShortListFolder();

            var shortLists = new List<ShortList>();

            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.GetFiles(dir, "*.json"))
                {
                    try
                    {
                        shortLists.Add(ShortList.LoadFromFile(file));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to load shortlist {file}: {ex.Message}");
                    }
                }
            }

            ShortLists.Clear();
            foreach (var shortList in shortLists)
            {
                ShortLists.Add(shortList);
            }
        }

        private void LoadConfiguration()
        {
            if (!File.Exists(_configFilePath))
            {
                _configuration = new Configuration();
                return;
            }

            _configuration = Configuration.LoadFromFile(_configFilePath);
            _configuration.LibraryRoots ??= [];
            _configuration.TagIgnoreList ??= [];
            LibraryRootPaths = string.Join(Environment.NewLine, _configuration.LibraryRoots);
            TagIgnoreList = string.Join(", ", _configuration.TagIgnoreList);
            ResultVisualizationMode = _configuration.ResultVisualizationMode;
        }

        private void SaveConfiguration()
        {
            _configuration.SaveToFile(_configFilePath);
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (_soundPlayer.IsPlaying)
            {
                var currentTime = _soundPlayer.CurrentTime!.Value;
                WaveformViewer.TimeCursor = currentTime;
                Console.WriteLine(currentTime);
            }
        }

        private Task<ScanProgress> ScanInBackground()
        {
            _soundPlayer.Stop();
            CurrentSoundFile = null;
            return Task.Run(() =>
            {
                var scanner = new LibraryScanner(_library);
                var progress = scanner.Scan(_configuration.LibraryRoots, (progress) => Dispatcher.Invoke(() =>
                {
                    var removeMsg = progress.DeletesDone > 0 ? $"- {progress.DeletesDone} sounds removed" : "";
                    ProgressVisibility = Visibility.Visible;
                    ScanTaskDone = progress.NewDone;
                    ScanTaskTotal = progress.NewTotal;
                    ScanProgressMessage = $"{progress.NewDone} of {progress.NewTotal} new sounds added {removeMsg}";
                }));

                InvalidateSearchableSoundsCache();
                ReprocessSearch();

                return progress;
            });
        }

        private async void ScanChangesLibrary_Click(object sender, RoutedEventArgs e)
        {
            var lastProgressUpdate = await ScanInBackground();
            ScanProgressMessage = $"{lastProgressUpdate.NewTotal} new - {lastProgressUpdate.DeletesDone} removed. Your SoundShelf now contains {_library.Sounds.Count} sound(s).";
            ProgressVisibility = Visibility.Hidden;
        }

        private async void RescanFullLibrary_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("This rebuilds your SoundShelf library from scratch. This may take a while.\n\nAre you sure?", "Clear SoundShelf library?", MessageBoxButton.YesNo, MessageBoxImage.Question) !=
                MessageBoxResult.Yes)
                return;

            _library.Clear();
            await ScanInBackground();
            ScanProgressMessage = $"Your SoundShelf now contains {_library.Sounds.Count} sound(s).";
        }

        private void ReprocessSearch()
        {
            EnsureSearchableSoundsCache();

            var filtered = GetSearchResults();

            ShowResultsInUI(filtered.ToList());
        }

        private IEnumerable<SearchableSound> GetSearchResults()
        {
            var queryParts = SearchQuery.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var searchableSounds = _searchableSoundsCache!;

            if (CurrentShortList != null)
            {
                searchableSounds = searchableSounds.Where(s => CurrentShortList.SoundIds.Any(id => id == s.SoundId)).ToList();
            }

            return queryParts.Any()
                ? searchableSounds!.Where(s => queryParts.All(s.SearchKey.Contains))
                : searchableSounds!;
        }

        private void EnsureSearchableSoundsCache()
        {
            _searchableSoundsCache ??= (ResultVisualizationMode switch
            {
                ResultVisualizationMode.Filename => _library.Sounds.Select(sound => new SearchableSound(SoundLibrary.GetSoundId(sound.FilePath), sound.FileName, GetTags(sound), sound)
                {
                    IsInShortList = IsInShortList(SoundLibrary.GetSoundId(sound.FilePath))
                }),
                ResultVisualizationMode.Title => _library.Sounds.Select(sound => new SearchableSound(SoundLibrary.GetSoundId(sound.FilePath), sound.MetaData?.Title ?? sound.FileName, GetTags(sound), sound)
                {
                    IsInShortList = IsInShortList(SoundLibrary.GetSoundId(sound.FilePath))
                }),
                ResultVisualizationMode.Comment => _library.Sounds.Select(sound => new SearchableSound(SoundLibrary.GetSoundId(sound.FilePath), sound.MetaData?.Comment ?? sound.FileName, GetTags(sound), sound)
                {
                    IsInShortList = IsInShortList(SoundLibrary.GetSoundId(sound.FilePath))
                }),
                _ => throw new NotSupportedException($"Unknown result visualization mode: {ResultVisualizationMode}")
            }).ToList();
        }

        private bool IsInShortList(string soundId)
        {
            return ShortLists.Any(sl => sl.SoundIds.Any(id => id == soundId));
        }

        private List<string> GetTags(SoundFile sound)
        {
            return sound.DetectedTags
                .Where(tag => !_configuration.TagIgnoreList!.Any(t => t.Equals(tag, StringComparison.InvariantCultureIgnoreCase)))
                .OrderBy(tag => tag)
                .ToList();
        }

        private void ShowResultsInUI(List<SearchableSound> searchHits)
        {
            Dispatcher.Invoke(() =>
            {
                CountLabel = $"{searchHits.Count} matche(s) found";
                Results.Clear();
                foreach (var hit in searchHits)
                {
                    Results.Add(hit);
                }
            });
        }

        private void SoundList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is SearchableSound selected)
            {
                CurrentSoundFile = selected.SoundFile;
                _soundPlayer.Load(selected.SoundFile.FilePath);
                _soundPlayer.Play();
            }
            else
            {
                CurrentSoundFile = null;
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_soundPlayer.IsPlaying)
                _soundPlayer.Stop();
            else
            {
                Play();
            }
        }

        private void Play()
        {
            if (CurrentSoundFile == null) return;

            if (WaveformViewer.HasSliceSelection)
            {
                _soundPlayer.Play(WaveformViewer.SliceStart, WaveformViewer.SliceEnd);
            }
            else
            {
                _soundPlayer.Play();
                _soundPlayer.JumpTo(TimeCursor);
            }
        }

        private void BrowseTab_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (_soundPlayer.IsPlaying)
                    _soundPlayer.Stop();
                else
                    Play();
            }
            else if (e.Key == Key.Escape)
            {
                ShortListPopup.IsOpen = false;
            }
        }

        private void ExportSelectionButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (CurrentSoundFile == null || !WaveformViewer.HasSliceSelection)
            {
                MessageBox.Show("Export slice only works when you selected a slice in the wave viewer.");
                return;
            }
            var fileDlg = new SaveFileDialog
            {
                Filter = "Wave files (*.wav)|*.wav",
                DefaultExt = ".wav",
                OverwritePrompt = true,
                FileName = Path.GetFileNameWithoutExtension(CurrentSoundFile.FileName) + "_slice.wav"
            };

            if (fileDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SoundSliceExporter.ExportSlice(CurrentSoundFile!.FilePath, fileDlg.FileName, WaveformViewer.SliceStart, WaveformViewer.SliceEnd - WaveformViewer.SliceStart);
            }
        }

        /// <summary>
        /// The SoundFile currently showing in the SoundViewer.
        /// </summary>
        public SoundFile? CurrentSoundFile
        {
            get => _currentSoundFile;
            set => SetField(ref _currentSoundFile, value);
        }

        public ObservableCollection<SearchableSound> Results { get; } = new();

        public int ScanTaskTotal
        {
            get => _scanTaskTotal;
            set => SetField(ref _scanTaskTotal, value);
        }

        public int ScanTaskDone
        {
            get => _scanTaskDone;
            set => SetField(ref _scanTaskDone, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    OnPropertyChanged();
                    Task.Run(ReprocessSearch);
                }
            }
        }

        public TimeSpan TimeCursor
        {
            get => _cursor;
            set
            {
                SetField(ref _cursor, value);
                _soundPlayer.JumpTo(value);
            }
        }

        public TimeSpan StopAtTime
        {
            get => _stopAtTime;
            set
            {
                SetField(ref _stopAtTime, value);
                _soundPlayer.Stop();
            }
        }

        public string CountLabel
        {
            get => _countLabel;
            set => SetField(ref _countLabel, value);
        }

        public string ScanProgressMessage
        {
            get => _scanProgressMessage;
            set => SetField(ref _scanProgressMessage, value);
        }

        public Visibility ProgressVisibility
        {
            get => _progressVisibility;
            set => SetField(ref _progressVisibility, value);
        }

        public string LibraryRootPaths
        {
            get => _libraryRootPaths;
            set
            {
                if (value == _libraryRootPaths) return;

                SetField(ref _libraryRootPaths, value);
                _configuration.LibraryRoots = value.Split("\n").Select(root => root.Trim()).ToList();
                SaveConfiguration();
            }
        }

        public string TagIgnoreList
        {
            get => _tagIgnoreList;
            set
            {
                if (_tagIgnoreList == value) return;

                SetField(ref _tagIgnoreList, value);
                _configuration.TagIgnoreList = value.Split([',', ';']).Select(root => root.Trim().ToLower()).ToList();
                SaveConfiguration();
                InvalidateSearchableSoundsCache();
                ReprocessSearch();
            }
        }

        private void InvalidateSearchableSoundsCache()
        {
            _searchableSoundsCache = null;
        }

        public ResultVisualizationMode ResultVisualizationMode
        {
            get => _resultVisualizationMode;
            set
            {
                if (value == _resultVisualizationMode) return;

                SetField(ref _resultVisualizationMode, value);
                _configuration.ResultVisualizationMode = value;
                SaveConfiguration();
                ReprocessSearch();
            }
        }

        public string NewShortListName
        {
            get => _newShortListName;
            set => SetField(ref _newShortListName, value);
        }

        public ObservableCollection<ShortList> ShortLists { get; set; } = new();

        #region PropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        public void Dispose()
        {
            _soundPlayer.Dispose();
        }

        private void AddShortlistButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewShortListName) || ShortLists.Any(sl => sl.Name.Equals(NewShortListName, StringComparison.InvariantCultureIgnoreCase)))
            {
                MessageBox.Show("Enter a unique name for the new ShortList first.", "Bad ShortList name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var path = GetShortListFolder();

            Directory.CreateDirectory(path);

            var shortlist = new ShortList
            {
                FilePath = Path.Combine(path, Guid.NewGuid() + ".json"),
                Name = NewShortListName,
                SoundIds = []
            };

            shortlist.SaveToFile();
            LoadShortLists();

            NewShortListName = "";
        }

        private void DeleteShortListButton_Click(object sender, RoutedEventArgs e)
        {
            var shortList = (e.Source as FrameworkElement)?.DataContext as ShortList;
            if (shortList != null)
            {
                if (shortList.SoundIds.Any() && MessageBox.Show($"Shortlist '{shortList.Name}' contains {shortList.SoundIds.Count} sound(s).\n\nAre you sure to delete it?",
                        "Delete non-empty shortlist?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                    return;

                File.Delete(shortList.FilePath);
                LoadShortLists();
                if (_searchableSoundsCache != null)
                    foreach (var searchableSound in _searchableSoundsCache)
                    {
                        searchableSound.IsInShortList = IsInShortList(searchableSound.SoundId);
                    }
            }
        }

        private static string GetShortListFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SoundShelf",
                "Shortlists");
        }

        private void ShowFullCatalogButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentShortList = null;
            ReprocessSearch();
        }

        private void ShowShortListButton_Click(object sender, RoutedEventArgs e)
        {
            var shortList = (e.Source as FrameworkElement)?.DataContext as ShortList;
            if (shortList != null)
            {
                CurrentShortList = shortList;
                ReprocessSearch();
            }
        }

        private void ShortListContextMenuButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var sound = (e.Source as FrameworkElement)?.DataContext as SearchableSound;

            if (sound == null) return;

            ToggleShortListPopup(sender as UIElement, sound);
        }

        private void ToggleShortListPopup(UIElement? sender, SearchableSound sound)
        {
            if (ShortListPopup.IsOpen)
            {
                ShortListPopup.IsOpen = false;
                return;
            }

            ShortListPopup.PlacementTarget = sender;
            ShortListPopup.IsOpen = true;
            ShortListMemberships.Clear();
            foreach (var shortList in ShortLists)
            {
                ShortListMemberships.Add(new ShortListMembership(sound, shortList, shortList.SoundIds.Contains(sound.SoundId)));
            }
        }

        private void ShortListCOntextMenuButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // prevent item from being selected when pressing the ShortList button
        }

        private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                ShortListPopup.IsOpen = false;
        }

        public ObservableCollection<ShortListMembership> ShortListMemberships { get; } = new();

        private void AddToShortList_Click(object sender, RoutedEventArgs e)
        {
            var membership = (e.Source as FrameworkElement)?.DataContext as ShortListMembership;
            if (membership == null) return;

            membership.ShortList.SoundIds.Add(membership.Sound.SoundId);
            membership.ShortList.SaveToFile();
            membership.Sound.IsInShortList = IsInShortList(membership.Sound.SoundId);
        }

        private void RemoveFromShortList_Click(object sender, RoutedEventArgs e)
        {
            var membership = (e.Source as FrameworkElement)?.DataContext as ShortListMembership;
            if (membership == null) return;

            membership.ShortList.SoundIds.Remove(membership.Sound.SoundId);
            membership.ShortList.SaveToFile();
            membership.Sound.IsInShortList = IsInShortList(membership.Sound.SoundId);
        }
    }
}