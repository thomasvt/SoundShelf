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
        private SoundFileInfo? _currentSound;
        private TimeSpan _cursor;
        private TimeSpan _stopAtTime;
        private string _scanProgressMessage;
        private Visibility _progressVisibility = Visibility.Hidden;
        private Configuration _configuration;
        private string _libraryRootPaths;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering += OnRendering;

            LoadConfiguration();
            _library.Load();
            ScanProgressMessage = $"Your SoundShelf contains {_library.Sounds.Count} sound(s).";
            ApplySearchFilter();
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

        private void RescanLibrary_Click(object sender, RoutedEventArgs e)
        {
            _soundPlayer.Stop();
            CurrentSound = null;

            Task.Run(() =>
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
                ScanProgressMessage = $"{progress.NewTotal} new - {progress.DeletesDone} removed. SoundShelf now contains {_library.Sounds.Count} sound(s).";
                ProgressVisibility = Visibility.Hidden;
            });

        }

        private void ClearLibrary_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("This clears your SoundShelf library. No sound files are physically deleted. Are you sure?", "Are you sure?", MessageBoxButton.YesNo, MessageBoxImage.Question) !=
                MessageBoxResult.Yes)
                return;

            _library.Clear();
            _library.Save();

            CurrentSound = null;
            ApplySearchFilter();

            MessageBox.Show("Your SoundShelf library is now empty. Configure some rootfolders and Scan to detect sound files on your computer.", "Library cleared", MessageBoxButton.OK, MessageBoxImage.Information);
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

            ShowResultsInUI(filtered);
        }

        private void ShowResultsInUI(List<SoundFileInfo> sounds)
        {
            Dispatcher.Invoke(() =>
            {
                CountLabel = $"{sounds.Count} sound(s)";
                Results.Clear();
                foreach (var sound in sounds)
                {
                    Results.Add(sound);
                }
            });
        }

        private void SoundList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is SoundFileInfo selected)
            {
                CurrentSound = selected;
                _soundPlayer.Load(selected.FilePath);
                _soundPlayer.Play();
            }
            else
            {
                CurrentSound = null;
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            Play();
        }

        private void Play()
        {
            if (CurrentSound == null) return;

            if (WaveformViewer.HasSelection)
            {
                _soundPlayer.Play(WaveformViewer.SelectionStart, WaveformViewer.SelectionEnd);
            }
            else
            {
                _soundPlayer.Play();
                _soundPlayer.JumpTo(TimeCursor);
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _soundPlayer.Stop();
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
            if (!WaveformViewer.HasSelection)
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
                SliceExporter.ExportSlice(CurrentSound.FilePath, fileDlg.FileName, WaveformViewer.SelectionStart, WaveformViewer.SelectionEnd - WaveformViewer.SelectionStart);
            }
        }

        public SoundFileInfo? CurrentSound
        {
            get => _currentSound;
            set => SetField(ref _currentSound, value);
        }

        public ObservableCollection<SoundFileInfo> Results { get; } = new();

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