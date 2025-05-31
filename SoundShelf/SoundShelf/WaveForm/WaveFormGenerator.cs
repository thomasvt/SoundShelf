using NAudio.Wave;

namespace SoundShelf.WaveForm
{
    internal class WaveForm
    {
        public float[] Samples { get; set; }
        public TimeSpan Duration { get; set; }
    }

    internal class WaveFormGenerator
    {
        public float[] ExtractWaveformSamples(string filePath, int points)
        {
            using var reader = new AudioFileReader(filePath);
            float[] result = new float[points];

            float[] buffer = new float[reader.WaveFormat.SampleRate];
            long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
            int samplesPerPoint = (int)(totalSamples / points);

            for (int i = 0; i < points; i++)
            {
                reader.Position = i * samplesPerPoint * sizeof(float);
                int read = reader.Read(buffer, 0, samplesPerPoint);

                double sumSquares = 0;
                for (int j = 0; j < read; j++)
                {
                    sumSquares += buffer[j] * buffer[j];
                }

                result[i] = (float)Math.Sqrt(sumSquares / read);
            }

            return result;
        }

        public float[] ExtractWaveformSamples2(string filePath, int points)
        {
            using var reader = new AudioFileReader(filePath); // returns float samples already
            var waveFormat = reader.WaveFormat;

            int bytesPerSample = waveFormat.BitsPerSample / 8;
            int channels = waveFormat.Channels;
            long totalSamples = reader.Length / bytesPerSample;

            float[] result = new float[points];
            int samplesPerPoint = (int)(totalSamples / points / channels);
            int readBlockSize = samplesPerPoint * channels;

            float[] buffer = new float[readBlockSize];

            for (int i = 0; i < points; i++)
            {
                int read = reader.Read(buffer, 0, readBlockSize);
                if (read == 0) break;

                // Optional: combine all channels into mono peak
                double sumSquares = 0;
                for (int j = 0; j < read; j++)
                {
                    sumSquares += buffer[j] * buffer[j];
                }

                result[i] = (float)Math.Sqrt(sumSquares / read);
            }

            return result;
        }

        public WaveForm ExtractWaveform3(string filePath, int points)
        {
            using var reader = new AudioFileReader(filePath); // float samples, interleaved
            var waveFormat = reader.WaveFormat;

            int channels = waveFormat.Channels;
            long totalSamples = reader.Length / (waveFormat.BitsPerSample / 8);
            long totalFrames = totalSamples / channels;

            float[] result = new float[points];
            int framesPerPoint = (int)(totalFrames / points);
            int samplesPerPoint = framesPerPoint * channels;

            float[] buffer = new float[samplesPerPoint];

            for (int i = 0; i < points; i++)
            {
                int read = reader.Read(buffer, 0, samplesPerPoint);
                if (read == 0) break;

                int framesRead = read / channels;
                double sumSquares = 0;

                for (int f = 0; f < framesRead; f++)
                {
                    float sumChannels = 0;
                    for (int c = 0; c < channels; c++)
                    {
                        sumChannels += buffer[f * channels + c];
                    }

                    float monoSample = sumChannels / channels; // average to mono
                    sumSquares += monoSample * monoSample;
                }

                result[i] = (float)Math.Sqrt(sumSquares / framesRead); // RMS for chunk
            }

            return new WaveForm()
            {
                Samples = result,
                Duration = reader.TotalTime
            };
        }
    }
}
