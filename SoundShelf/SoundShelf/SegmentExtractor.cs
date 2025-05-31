using NAudio.Wave;

namespace SoundShelf
{
    internal class SegmentExtractor
    {
        public void ExtractSegmentToWav(string sourcePath, string outputPath, TimeSpan startTime, TimeSpan duration)
        {
            using var reader = new AudioFileReader(sourcePath);

            // Convert times to byte positions
            var waveFormat = reader.WaveFormat;
            int bytesPerSample = waveFormat.BitsPerSample / 8 * waveFormat.Channels;

            long startBytes = (long)(startTime.TotalSeconds * waveFormat.SampleRate) * bytesPerSample;
            long bytesToCopy = (long)(duration.TotalSeconds * waveFormat.SampleRate) * bytesPerSample;

            reader.Position = Math.Min(startBytes, reader.Length);

            using var writer = new WaveFileWriter(outputPath, waveFormat);

            byte[] buffer = new byte[1024 * bytesPerSample];
            long bytesRemaining = bytesToCopy;

            while (bytesRemaining > 0)
            {
                int bytesToRead = (int)Math.Min(buffer.Length, bytesRemaining);
                int bytesRead = reader.Read(buffer, 0, bytesToRead);
                if (bytesRead == 0)
                    break;

                writer.Write(buffer, 0, bytesRead);
                bytesRemaining -= bytesRead;
            }
        }
    }
}
