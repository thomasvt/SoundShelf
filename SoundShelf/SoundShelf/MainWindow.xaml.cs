using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using File = System.IO.File;
using Path = System.IO.Path;

namespace SoundShelf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
    {
        private readonly SoundPlayer _soundPlayer = new();
        private List<SoundFileInfo> _filterResults;
        private int _scanTaskTotal;
        private int _scanTaskDone;
        private readonly SoundLibrary _soundLibrary = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundShelf", "index.json"));
        private readonly string _configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundShelf", "config.json");
        private string _searchQuery = "";
        private string _countLabel;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering += OnRendering;

            if (!File.Exists(_configFilePath))
            {
                var configuration = new Configuration();
                configuration.LibraryRoots.Add("C:\\sounds");
                configuration.SaveToFile(_configFilePath);
            }
            else
            {
                var configuration = Configuration.LoadFromFile(_configFilePath);
                foreach (var libraryRoot in configuration.LibraryRoots)
                {
                    _soundLibrary.AddLibraryRoot(libraryRoot);
                }
                _soundLibrary.LoadIndex();
                ShowResultsInUI(_soundLibrary.Sounds);
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (_soundPlayer.IsPlaying)
            {
                var currentTime = _soundPlayer.CurrentTime!.Value;
                WaveformViewer.Cursor = currentTime;
            }
        }

        private void RefreshLibrary_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                _soundLibrary.Sync((done, total) => Dispatcher.Invoke(() =>
                {
                    ScanTaskDone = done;
                    ScanTaskTotal = total;
                }));
                Results = _soundLibrary.Sounds;
            });
        }

        private void ConfigureLibrary_Click(object sender, RoutedEventArgs e)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo(_configFilePath) { UseShellExecute = true }
            };

            process.Start();
        }

        private void ApplySearchFilter()
        {
            var query = SearchQuery.Trim().ToLowerInvariant();

            var filtered = string.IsNullOrWhiteSpace(query)
                ? _soundLibrary.Sounds
                : _soundLibrary.Sounds.FindAll(s =>
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
                return Results = sounds;
            });
        }

        private void SoundList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is SoundFileInfo selected)
            {
                CurrentSound = selected;
                _soundPlayer.Play(selected.FilePath);
                WaveformViewer.SoundFilePath = selected.FilePath;
            }
            else
            {
                CurrentSound = null;
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSound == null) return;

            if (WaveformViewer.HasSelection)
            {
                _soundPlayer.Play(CurrentSound.FilePath, WaveformViewer.SelectionStart, WaveformViewer.SelectionEnd);
            }
            else
            {
                _soundPlayer.Play(CurrentSound.FilePath);
            }
        }


        public SoundFileInfo? CurrentSound { get; set; }

        public List<SoundFileInfo> Results
        {
            get => _filterResults;
            set => SetField(ref _filterResults, value);
        }

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

        public string CountLabel
        {
            get => _countLabel;
            set => SetField(ref _countLabel, value);
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