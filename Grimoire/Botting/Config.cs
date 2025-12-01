using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Grimoire.Botting
{
    public class Config
    {
        public string file { get; set; }

        public Dictionary<string, string> Contents { get; set; } = new Dictionary<string, string>();

        public Config()
        {

        }

        public static Config Instance = new Config();

        public string Get(string key)
        {
            return Contents.TryGetValue(key, out string s) ? s : null;
        }

        public void Set(string key, string value)
        {
            Contents[key] = value;
        }

        public void Save()
        {
            File.WriteAllLines(file, Contents.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }

        public static Config Load(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    using (File.Create(path)) { }
                }

                var lines = File.Exists(path) ? File.ReadLines(path) : Enumerable.Empty<string>();
                var dict = lines
                    .Select(l => l.Split(new[] { '=' }, 2))
                    .Where(parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
                    .ToDictionary(parts => parts[0], parts => parts[1]);

                return new Config()
                {
                    file = path,
                    Contents = dict
                };
            }
            catch
            {
                return new Config()
                {
                    file = path,
                    Contents = new Dictionary<string, string>()
                };
            }
        }
    }
}