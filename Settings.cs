using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ROBdk97.XmlDocToMd
{
    /// <summary>
    /// Json Settings file for the XmlDocToMd
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// Wich xml files to ignore when converting to markdown
        /// </summary>
        public List<string> FilesToIgnore { get; set; } = [];
        /// <summary>
        /// Wich namespaces to ignore when converting to markdown
        /// </summary>
        public List<string> NameSpacesToRemove { get; set; } = [];
    }


    internal static class SettingsExt
    {
        // Load settings from file via json
        public static Settings LoadSettings(string path)
        {
            Console.WriteLine($"Loading settings from \"{Path.GetFullPath(path)}\".");
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    // Console creating settings
                    Console.WriteLine($"Settings file \"{path}\" does not exist. Creating a new one.");
                    // create a new settings file
                    System.IO.File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new Settings()));
                }
                // Use System.Text.Json to load the settings from the file
                return System.Text.Json.JsonSerializer.Deserialize<Settings>(System.IO.File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings file \"{path}\": {ex.Message}");
                return new Settings();
            }
        }

    }
}
