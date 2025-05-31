using System.IO;
using System.Text.Json;
using System.Windows.Shapes;
using Path = System.IO.Path;

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

        public void Clear()
        {
            _knownSoundFiles.Clear();
            Sounds.Clear();
            SaveIndex();
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
            var soundFiles = _libraryRoots.SelectMany(lr => Directory.EnumerateFiles(lr, "*.wav", SearchOption.AllDirectories)).ToHashSet();

            // remove disappeared sounds
            foreach (var soundFile in _knownSoundFiles)
            {
                var soundId = GetSoundId(soundFile);
                if (!soundFiles.Contains(soundId))
                {
                    Sounds.RemoveAt(Sounds.FindIndex(info => GetSoundId(info.FilePath) == soundId));
                    _knownSoundFiles.Remove(soundId);
                }
            }

            var newSoundFiles = soundFiles.Where(s => !_knownSoundFiles.Contains(GetSoundId(s))).ToList();
            progressUpdateAction.Invoke(0, newSoundFiles.Count);
            var i = 0;

            // add new sounds.
            foreach (var path in newSoundFiles)
            {
                var soundId = GetSoundId(path);
                if (_knownSoundFiles.Contains(soundId))
                    continue;

                var info = new SoundFileInfo
                {
                    FilePath = path,
                    FileName = Path.GetFileNameWithoutExtension(path),
                    MetaData = SoundFileMetaData.ReadFrom(path)
                };

                Sounds.Add(info);
                _knownSoundFiles.Add(soundId);
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
