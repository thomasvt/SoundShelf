using System.IO;
using System.Text.Json;

namespace SoundShelf
{
    public class Configuration
    {
        public List<string>? LibraryRoots { get; set; } = [];
        public List<string>? TagIgnoreList { get; set; } = [];
        public ResultVisualizationMode ResultVisualizationMode { get; set; } = ResultVisualizationMode.Filename;

        public static Configuration LoadFromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Configuration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new Configuration(); // Fallback if deserialization fails
        }

        public void SaveToFile(string path)
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
        }
    }
}
