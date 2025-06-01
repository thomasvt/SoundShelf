using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using SoundShelf.Library;
using File = System.IO.File;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace SoundShelf
{
    public record SearchHit(string Label, SoundFile SoundFile);
        
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
    {
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
            ScanProgressMessage = $"Your SoundShelf contains {_library.Sounds.Count} sound(s).";
            ApplySearchFilter();
        }

        private void LoadSoundLibrary()
        {
            try
            {
                _library.Load();
            }
            catch (Exception e)
            {
                MessageBox.Show(
                    "Could not load library from index file. It is probably corrupt. If you manually changed the index file, fix it; or Clean the library from the Manage tab in SoundShelf.\n\nError:\n\n" + e.Message,
                    "Library corrupt", MessageBoxButton.OK, MessageBoxImage.Error);
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
            LibraryRootPaths = string.Join(Environment.NewLine, _configuration.LibraryRoots);
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

                ApplySearchFilter();
                
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

        private void ApplySearchFilter()
        {
            var query = SearchQuery.Trim().ToLowerInvariant();

            var filtered = string.IsNullOrWhiteSpace(query)
                ? _library.Sounds
                : _library.Sounds.FindAll(s =>
                    s.FileName.ToLowerInvariant().Contains(query) ||
                    s.MetaData?.Title?.ToLowerInvariant().Contains(query) == true ||
                    s.MetaData?.Artist?.ToLowerInvariant().Contains(query) == true);


            var results = ResultVisualizationMode switch
            {
                ResultVisualizationMode.Filename => filtered.Select(sound => new SearchHit(sound.FileName, sound)),
                ResultVisualizationMode.Title => filtered.Select(sound => new SearchHit(sound.MetaData?.Title ?? "<?>", sound)),
                ResultVisualizationMode.Comment => filtered.Select(sound => new SearchHit(sound.MetaData?.Comment ?? "<?>", sound)),
                _ => throw new NotSupportedException($"Unknown result visualization mode: {ResultVisualizationMode}")
            };

            ShowResultsInUI(results.ToList());
        }

        private void ShowResultsInUI(List<SearchHit> searchHits)
        {
            Dispatcher.Invoke(() =>
            {
                CountLabel = $"{searchHits.Count} sound(s)";
                Results.Clear();
                foreach (var hit in searchHits)
                {
                    Results.Add(hit);
                }
            });
        }

        private void SoundList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is SearchHit selected)
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

        private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (_soundPlayer.IsPlaying)
                    _soundPlayer.Stop();
                else
                    Play();
            }
        }

        private void ExportSelectionButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!WaveformViewer.HasSliceSelection)
            {
                MessageBox.Show("Export slice only works when you selected a slice of the wave.");
                return;
            }
            var fileDlg = new SaveFileDialog
            {
                Filter = "Wave files (*.wav)|*.wav",
                DefaultExt = ".wav",
                OverwritePrompt = true,
                FileName = "slice.wav"
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

        public ObservableCollection<SearchHit> Results { get; } = new();

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
                    Task.Run(ApplySearchFilter);
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

        public ResultVisualizationMode ResultVisualizationMode
        {
            get => _resultVisualizationMode;
            set
            {
                if (value == _resultVisualizationMode) return;

                SetField(ref _resultVisualizationMode, value);
                _configuration.ResultVisualizationMode = value;
                SaveConfiguration();
                ApplySearchFilter();
            }
        }

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


    }
}