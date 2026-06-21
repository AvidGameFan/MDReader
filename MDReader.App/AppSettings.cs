using System;
using System.IO;
using System.Text.Json;

namespace MDReader.App;

public sealed class AppSettings
{
    public bool SoftLineBreaksAsHard { get; set; }
    public bool IgnoreEditRoundTripWarning { get; set; }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MDReader",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var json = JsonSerializer.Serialize(this);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore settings save errors
        }
    }
}
