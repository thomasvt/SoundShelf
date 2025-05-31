using NAudio.Wave;

namespace SoundShelf
{
    internal static class SliceExporter
    {
        public static void ExportSlice(string sourcePath, string outputPath, TimeSpan startTime, TimeSpan duration)
        {
            using var reader = new AudioFileReader(sourcePath);

            // Convert times to byte positions
            var waveFormat = reader.WaveFormat;
            var bytesPerSample = waveFormat.BitsPerSample / 8 * waveFormat.Channels;

            var startBytes = (long)(startTime.TotalSeconds * waveFormat.SampleRate) * bytesPerSample;
            var bytesToCopy = (long)(duration.TotalSeconds * waveFormat.SampleRate) * bytesPerSample;

            reader.Position = Math.Min(startBytes, reader.Length);

            using var writer = new WaveFileWriter(outputPath, waveFormat);

            var buffer = new byte[1024 * bytesPerSample];
            var bytesRemaining = bytesToCopy;

            while (bytesRemaining > 0)
            {
                var bytesToRead = (int)Math.Min(buffer.Length, bytesRemaining);
                var bytesRead = reader.Read(buffer, 0, bytesToRead);
                if (bytesRead == 0)
                    break;

                writer.Write(buffer, 0, bytesRead);
                bytesRemaining -= bytesRead;
            }
            writer.Flush();
        }
    }
}
