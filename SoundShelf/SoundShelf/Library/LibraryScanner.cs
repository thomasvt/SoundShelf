using System.IO;

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
                .Select(SoundLibrary.GetSoundId)
                .ToHashSet();

            var deleteCount = 0;

            // remove disappeared sounds
            foreach (var soundFile in library.KnownSoundFiles)
            {
                var soundId = SoundLibrary.GetSoundId(soundFile);
                if (!soundFiles.Contains(soundId))
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

                var info = new SoundFileInfo
                {
                    FilePath = path,
                    FileName = Path.GetFileNameWithoutExtension(path),
                    MetaData = SoundFileMetaData.ReadFrom(path)
                };

                library.Sounds.Add(info);
                library.KnownSoundFiles.Add(soundId);
                progressUpdateAction.Invoke(new ScanProgress(newSoundFiles.Count, ++i, deleteCount));
            }

            library.Save();
            return new ScanProgress(newSoundFiles.Count, newSoundFiles.Count, deleteCount);
        }
    }
}
