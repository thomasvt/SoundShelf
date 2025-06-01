using System.IO;
using TagLib.Flac;

namespace SoundShelf.Library
{
    public record ScanProgress(int NewTotal, int NewDone, int DeletesDone);

    public class LibraryScanner(SoundLibrary library)
    {
        /// <summary>
        /// Detects new and removed sounds on disk and adjusts the library accordingly. Saves the library to file.
        /// </summary>
        public ScanProgress Scan(IEnumerable<string> libraryRoots, Action<ScanProgress> progressUpdateAction)
        {
            var soundFiles = libraryRoots
                .SelectMany(lr => Directory.EnumerateFiles(lr, "*.wav", SearchOption.AllDirectories))
                .ToList();

            var soundFilesLookup = soundFiles
                .Select(SoundLibrary.GetSoundId)
                .ToHashSet();

            var deleteCount = 0;

            // remove disappeared sounds
            foreach (var soundFile in library.KnownSoundFiles)
            {
                var soundId = SoundLibrary.GetSoundId(soundFile);
                if (!soundFilesLookup.Contains(soundId))
                {
                    library.Sounds.RemoveAt(library.Sounds.FindIndex(info => SoundLibrary.GetSoundId(info.FilePath) == soundId));
                    library.KnownSoundFiles.Remove(soundId);
                    deleteCount++;
                }
            }

            // add new sounds.
            var newSoundFiles = soundFiles.Where(s => !library.KnownSoundFiles.Contains(SoundLibrary.GetSoundId(s))).ToList();
            progressUpdateAction.Invoke(new ScanProgress(newSoundFiles.Count, 0, deleteCount));
            var i = 0;

            foreach (var path in newSoundFiles)
            {
                var soundId = SoundLibrary.GetSoundId(path);
                if (library.KnownSoundFiles.Contains(soundId))
                    continue;

                var metaData = SoundFileMetaData.ReadFrom(path);

                var filename = Path.GetFileNameWithoutExtension(path);
                var info = new SoundFile
                {
                    DetectedTags = GetTags(filename, metaData),
                    FilePath = path,
                    FileName = filename,
                    MetaData = metaData
                };

                library.Sounds.Add(info);
                library.KnownSoundFiles.Add(soundId);
                progressUpdateAction.Invoke(new ScanProgress(newSoundFiles.Count, ++i, deleteCount));
            }

            library.Save();
            return new ScanProgress(newSoundFiles.Count, newSoundFiles.Count, deleteCount);
        }

        /// <summary>
        /// Returns all individual textual tags that can be compiled from filename and metadata.
        /// </summary>
        private List<string> GetTags(string fileName, SoundFileMetaData? metaData)
        {
            var tags = new List<string>();
            AddIfNotEmpty(tags, metaData?.Artist);
            AddIfNotEmpty(tags, metaData?.Genre);
            SplitComplexString(tags, metaData?.Title);
            SplitComplexString(tags, fileName);

            return tags;
        }

        private void SplitComplexString(List<string> tags, string? complex)
        {
            if (complex == null) return;

            var parts = complex
                .Split(['_', '-', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0);

            foreach (var part in parts)
            {
                AddIfDistinct(tags, part);
            }

        }

        private static void AddIfNotEmpty(List<string> tags, string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            AddIfDistinct(tags, tag);
        }

        private static void AddIfDistinct(List<string> tags, string tag)
        {
            tag = tag.ToLower();
            if (tags.Contains(tag)) return;
            tags.Add(tag);
        }
    }
}
