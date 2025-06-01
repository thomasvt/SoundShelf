using System.IO;
using System.Text.Json;
using Path = System.IO.Path;

namespace SoundShelf.Library
{
    public class SoundLibrary(string indexFilePath)
    {
        internal readonly HashSet<string> KnownSoundFiles = new();
        public List<SoundFileInfo> Sounds { get; } = new();

        public void Clear()
        {
            KnownSoundFiles.Clear();
            Sounds.Clear();
        }

        public void Load()
        {
            if (!File.Exists(indexFilePath))
                return;

            var json = File.ReadAllText(indexFilePath);
            var sounds = JsonSerializer.Deserialize<List<SoundFileInfo>>(json);

            Clear();
            foreach (var sound in sounds!)
            {
                Sounds.Add(sound);
                KnownSoundFiles.Add(GetSoundId(sound.FilePath));
            }
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(Sounds, new JsonSerializerOptions() { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(indexFilePath));
            File.WriteAllText(indexFilePath, json);
        }

        public static string GetSoundId(string filePath)
        {
            return filePath.ToLower();
        }
    }
}
