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
        if (string.IsNullOrEmpty(Current.Pin))
        {
            Current.Pin = Random.Shared.Next(100000, 999999).ToString();
            Save();
        }
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
