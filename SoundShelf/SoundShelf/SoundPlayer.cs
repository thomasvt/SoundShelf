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
        
        private TimeSpan? _startAtTime;

        public SoundPlayer()
        {
            _playbackTimer.Tick += PlaybackTimer_Tick;
            _playbackTimer.Start();
        }
        
        public void Load(string filePath)
        {
            try
            {
                _audioFileReader?.Dispose();
                _wavePlayer.Stop();

                _audioFileReader = new AudioFileReader(filePath);

                _wavePlayer.Init(_audioFileReader);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load sound {filePath}: {ex.Message}");
            }
        }

        public void Play(TimeSpan? startAt = null, TimeSpan? stopAt = null)
        {
            try
            {
                _wavePlayer.Stop();
                _wavePlayer.Play();

                _startAtTime = startAt;

                _audioFileReader!.CurrentTime = _startAtTime ?? TimeSpan.Zero;

                StopAtTime = stopAt;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to play sound: {ex.Message}");
            }
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (_audioFileReader == null || StopAtTime == null) return;

            if (_audioFileReader!.CurrentTime >= StopAtTime)
            {
                _wavePlayer.Stop();
            }
        }

        public void Stop()
        {
            _wavePlayer.Stop();
        }

        public void JumpTo(TimeSpan time)
        {
            _audioFileReader!.CurrentTime = time;
        }

        public bool IsPlaying => _wavePlayer.PlaybackState == PlaybackState.Playing;

        public TimeSpan? CurrentTime => _wavePlayer.PlaybackState == PlaybackState.Stopped ? (_startAtTime ?? TimeSpan.Zero) : _audioFileReader?.CurrentTime;

        public TimeSpan? TotalTime => _audioFileReader?.TotalTime;

        public TimeSpan? StopAtTime { get; set; }

        public void Dispose()
        {
            _wavePlayer.Dispose();
            _audioFileReader?.Dispose();
        }
    }
}
