using System.IO;
using System.Text.Json;

namespace CheckAlarmNow.Core;

public class AppSettings
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CheckAlarmNow");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public int PatienceMinutes { get; set; } = 5;
    public List<string> MonitoredApps { get; set; } = new();
    public int CheckIntervalSeconds { get; set; } = 3;
    public bool SoundEnabled { get; set; } = true;
    public double PetSize { get; set; } = 1.0;
    public double PetPositionX { get; set; } = double.NaN;
    public double PetPositionY { get; set; } = double.NaN;
    public string? IdleImagePath { get; set; }
    public string? WarnImagePath { get; set; }
    public string? AlertImagePath { get; set; }
    public bool StartWithWindows { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
