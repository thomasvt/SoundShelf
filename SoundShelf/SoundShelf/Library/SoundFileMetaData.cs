namespace SoundShelf.Library
{
    public class SoundFileMetaData
    {
        // From TagLib.Tag
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Comment { get; set; }
        public string? Album { get; set; }
        public string? Genre { get; set; }
        public uint? Year { get; set; }
        public uint? Track { get; set; }
        public uint? TrackCount { get; set; }
        public string? Lyrics { get; set; }
        public string? Composer { get; set; }
        public string? Conductor { get; set; }
        public string? Copyright { get; set; }

        // From TagLib.Riff.File.Tag
        public string? Description { get; set; }
        public string? Software { get; set; }

        // From TagLib.Properties
        public TimeSpan? Duration { get; set; }
        public int? AudioBitrate { get; set; }
        public int? AudioSampleRate { get; set; }
        public int? Channels { get; set; }

        public static SoundFileMetaData? ReadFrom(string file)
        {
            try
            {
                var tagFile = TagLib.File.Create(file) as TagLib.Riff.File;
                if (tagFile == null)
                    return null;

                var tag = tagFile.Tag;
                var props = tagFile.Properties;

                return new SoundFileMetaData
                {
                    Title = string.IsNullOrWhiteSpace(tag.Title) ? null : tag.Title,
                    Artist = tag.FirstPerformer,
                    Comment = tag.Comment,
                    Album = tag.Album,
                    Genre = tag.FirstGenre,
                    Year = tag.Year != 0 ? tag.Year : null,
                    Track = tag.Track != 0 ? tag.Track : null,
                    TrackCount = tag.TrackCount != 0 ? tag.TrackCount : null,
                    Lyrics = tag.Lyrics,
                    Composer = tag.FirstComposer,
                    Conductor = tag.Conductor,
                    Copyright = tag.Copyright,
                    Description = tag.Description,
                    Software = props?.Description,
                    Duration = props?.Duration,
                    AudioBitrate = props?.AudioBitrate,
                    AudioSampleRate = props?.AudioSampleRate,
                    Channels = props?.AudioChannels
                };
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
