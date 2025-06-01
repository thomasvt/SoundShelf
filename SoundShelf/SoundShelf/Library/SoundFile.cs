namespace SoundShelf.Library
{
    public class SoundFile
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public List<string> DetectedTags { get; set; }
        public SoundFileMetaData? MetaData { get; set; }
    }
}
