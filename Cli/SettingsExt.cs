namespace ROBdk97.XmlDocToMd.Cli;

/// <summary>
/// Provides loading and auto-creation of <see cref="Settings"/> from a JSON file.
/// </summary>
internal static class SettingsExt
{
    /// <summary>
    /// Loads a <see cref="Settings"/> instance from <paramref name="path"/>.
    /// Creates the file with default values when it does not exist.
    /// </summary>
    /// <param name="path">Path to the JSON settings file.</param>
    /// <returns>
    /// The deserialised <see cref="Settings"/>, or a default instance if the file
    /// cannot be read.
    /// </returns>
    /// <note>
    /// Errors during loading are reported to <see cref="Console.Out"/> and result in a
    /// silent fallback to empty defaults rather than terminating the process.
    /// </note>
    public static Settings LoadSettings(string path)
    {
        Console.WriteLine($"Loading settings from \"{Path.GetFullPath(path)}\".");
        try
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"Settings file \"{path}\" does not exist. Creating a new one.");
                File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new Settings()));
            }
            Settings? deserialized = System.Text.Json.JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
            return deserialized ?? new Settings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings file \"{path}\": {ex.Message}");
            return new Settings();
        }
    }
}
