using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;

namespace SoundShelf
{
    internal class SoundPlayer : IDisposable
    {
        private readonly IWavePlayer _wavePlayer = new WaveOutEvent();
        private AudioFileReader? _audioFileReader;
        private readonly DispatcherTimer _playbackTimer = new() { Interval = TimeSpan.FromMilliseconds(10) };
        private TimeSpan? _stopAtTime;

        public SoundPlayer()
        {
            _playbackTimer.Tick += PlaybackTimer_Tick;
            _playbackTimer.Start();
        }
        
        public void Play(string filePath, TimeSpan? startAt = null, TimeSpan? stopAt = null)
        {
            try
            {
                _audioFileReader?.Dispose();
                _wavePlayer.Stop();

                _audioFileReader = new AudioFileReader(filePath);

                _wavePlayer.Init(_audioFileReader);
                _wavePlayer.Play();

                if (startAt.HasValue)
                    _audioFileReader.CurrentTime = startAt.Value;

                _stopAtTime = stopAt;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to play sound: {ex.Message}");
            }
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (_audioFileReader == null || _stopAtTime == null) return;

            if (_audioFileReader!.CurrentTime >= _stopAtTime)
            {
                _wavePlayer.Stop();
            }
        }

        public bool IsPlaying => _wavePlayer.PlaybackState == PlaybackState.Playing;

        public TimeSpan? CurrentTime => _wavePlayer.PlaybackState == PlaybackState.Stopped ? TimeSpan.Zero : _audioFileReader?.CurrentTime;

        public TimeSpan? TotalTime => _audioFileReader?.TotalTime;

        public event Action? PlaybackStopped;

        public void Dispose()
        {
            _wavePlayer.Dispose();
            _audioFileReader?.Dispose();
        }
    }
}
