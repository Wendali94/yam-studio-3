using System.Text.Json;

namespace YamStudio.Core;

public static class SettingsStore
{
    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YamStudio");
    static readonly string FilePath = Path.Combine(Dir, "settings.json");
    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { Current = new AppSettings(); }
        if (string.IsNullOrWhiteSpace(Current.RootDir)) Current.RootDir = @"C:\YamStudio";
        Loc.Lang = Current.Lang;
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
