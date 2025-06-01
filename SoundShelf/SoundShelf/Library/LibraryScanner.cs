using System.IO;

namespace SoundShelf.Library
{
    public record ScanProgress(int NewTotal, int NewDone, int DeletesDone);

    public class LibraryScanner(SoundLibrary library)
    {
        private record ScanFile(string Path, string RootFolder);

        /// <summary>
        /// Detects new and removed sounds on disk and adjusts the library accordingly. Saves the library to file.
        /// </summary>
        public ScanProgress Scan(IEnumerable<string> libraryRoots, Action<ScanProgress> progressUpdateAction)
        {
            var soundFiles = libraryRoots
                .SelectMany(lr => Directory.EnumerateFiles(lr, "*.wav", SearchOption.AllDirectories).Select(f => new ScanFile(f, lr)))
                .ToList();

            var soundFilesLookup = soundFiles
                .Select(sf => SoundLibrary.GetSoundId(sf.Path))
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
            var newSoundFiles = soundFiles.Where(s => !library.KnownSoundFiles.Contains(SoundLibrary.GetSoundId(s.Path))).ToList();
            progressUpdateAction.Invoke(new ScanProgress(newSoundFiles.Count, 0, deleteCount));
            var i = 0;

            foreach (var scanFile in newSoundFiles)
            {
                var path = scanFile.Path;
                var soundId = SoundLibrary.GetSoundId(path);
                if (library.KnownSoundFiles.Contains(soundId))
                    continue;

                var metaData = SoundFileMetaData.ReadFrom(path);
                var filename = Path.GetFileNameWithoutExtension(path);
                var pathFromRoot = Path.GetRelativePath(scanFile.RootFolder, Path.GetDirectoryName(scanFile.Path)!);

                var info = new SoundFile
                {
                    DetectedTags = GetTags(filename, metaData, pathFromRoot),
                    FilePath = path,
                    FileName = filename,
                    PathFromRoot = pathFromRoot,
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
        private List<string> GetTags(string fileName, SoundFileMetaData? metaData, string pathFromRoot)
        {
            var tags = new List<string>();
            AddIfNotEmpty(tags, metaData?.Artist);
            AddIfNotEmpty(tags, metaData?.Genre);
            AddTagsFromComplexString(tags, metaData?.Title);
            AddTagsFromComplexString(tags, fileName);
            foreach (var subFolder in pathFromRoot.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
            {
                AddIfDistinct(tags, "Folder:" + subFolder);
            }

            return tags;
        }

        private static void AddTagsFromComplexString(List<string> tags, string? complex)
        {
            if (complex == null) return;

            var parts = complex
                .Split(['_', '-', ' ', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
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
            if (tags.Any(t => string.Equals(t, tag, StringComparison.CurrentCultureIgnoreCase))) return;
            tags.Add(tag);
        }
    }
}
