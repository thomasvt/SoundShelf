using System.IO;
using System.Text.Json;

namespace SoundShelf
{
    public class SoundLibrary
    {
        private readonly string _indexFilePath;
        private readonly HashSet<string> _knownSoundFiles = new();

        private readonly List<string> _libraryRoots = new();
        public List<SoundFileInfo> Sounds { get; } = new();

        public SoundLibrary(string indexFilePath)
        {
            _indexFilePath = indexFilePath;
        }

        public void LoadIndex()
        {
            if (!File.Exists(_indexFilePath))
                return;

            var json = File.ReadAllText(_indexFilePath);
            var sounds = JsonSerializer.Deserialize<List<SoundFileInfo>>(json);

            Sounds.Clear();
            Sounds.AddRange(sounds);

            _knownSoundFiles.Clear();
            foreach (var sound in sounds)
            {
                _knownSoundFiles.Add(GetSoundId(sound.FilePath));
            }
        }

        public void Sync(Action<int, int> progressUpdateAction)
        {
            var soundFiles = _libraryRoots.SelectMany(lr => Directory.EnumerateFiles(lr, "*.wav", SearchOption.AllDirectories)).ToList();

            progressUpdateAction.Invoke(0, soundFiles.Count);
            var i = 0;

            foreach (var path in soundFiles)
            {
                var id = GetSoundId(path);
                if (_knownSoundFiles.Contains(id))
                    continue;

                var info = new SoundFileInfo
                {
                    FilePath = path,
                    FileName = Path.GetFileNameWithoutExtension(path),
                    MetaData = SoundFileMetaData.ReadFrom(path)
                };

                Sounds.Add(info);
                _knownSoundFiles.Add(id);
                progressUpdateAction.Invoke(++i, soundFiles.Count);
            }

            SaveIndex();
        }

        private void SaveIndex()
        {
            var json = JsonSerializer.Serialize(Sounds, new JsonSerializerOptions() { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(_indexFilePath));
            File.WriteAllText(_indexFilePath, json);
        }

        private static string GetSoundId(string filePath)
        {
            return filePath.ToLower();
        }

        public void AddLibraryRoot(string libraryRoot)
        {
            _libraryRoots.Add(libraryRoot);
        }
    }
}
