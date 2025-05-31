using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
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
        private List<SoundFileInfo> _filterResults;
        private int _scanTaskTotal;
        private int _scanTaskDone;
        private readonly SoundLibrary _soundLibrary = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundShelf", "index.json"));
        private readonly string _configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundShelf", "config.json");
        private string _searchQuery = "";
        private string _countLabel;
        private SoundFileInfo? _currentSound;
        private TimeSpan _cursor;
        private TimeSpan _stopAtTime;
        private string _scanProgressMessage;

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
                WaveformViewer.TimeCursor = currentTime;
            }
        }

        private void RescanLibrary_Click(object sender, RoutedEventArgs e)
        {
            _soundPlayer.Stop();
            CurrentSound = null;

            Task.Run(() =>
            {
                _soundLibrary.Sync((done, total) => Dispatcher.Invoke(() =>
                {
                    ScanTaskDone = done;
                    ScanTaskTotal = total;
                    ScanProgressMessage = $"{done} / {total}";
                }));
                Dispatcher.Invoke(ApplySearchFilter);
            });
        }

        private void ClearLibrary_Click(object sender, RoutedEventArgs e)
        {
            _soundLibrary.Clear();
            CurrentSound = null;
            ApplySearchFilter();
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

        public ObservableCollection<SoundFileInfo> Results { get; }= new();

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