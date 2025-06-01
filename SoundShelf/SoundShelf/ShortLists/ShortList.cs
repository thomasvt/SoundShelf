using System.IO;
using System.Text.Json;

namespace SoundShelf.ShortLists
{
    public class ShortList
    {
        public string FilePath { get; set; }
        public string Name { get; set; }
        public List<string> SoundIds { get; set; }

        public static ShortList LoadFromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ShortList>(json) ?? throw new Exception($"ShortList '{path}' is corrupt.");
        }

        public void SaveToFile()
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, json);
        }
    }
}
