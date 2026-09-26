using System.Text.Json;

namespace ScreenshotBox.Linux;

public sealed class AppSettings
{
    public string Language { get; set; } = "System";
    public string Theme { get; set; } = "System";
    public string Shortcut { get; set; } = "Ctrl+Alt+S";
    public string DataDirectory { get; set; } = DefaultDataDirectory;
    public double ThumbnailSize { get; set; } = 200;
    public string PenColor { get; set; } = "#EF4444";
    public int PenWidth { get; set; } = 4;
    public int EraserWidth { get; set; } = 24;
    public int MosaicSize { get; set; } = 12;
    public static string DefaultDataDirectory => Path.Combine(Xdg("XDG_DATA_HOME", ".local/share"), "ScreenshotBox", "library");
    public static string SettingsPath => Path.Combine(Xdg("XDG_CONFIG_HOME", ".config"), "ScreenshotBox", "settings.json");
    private static string Xdg(string name, string fallback) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } path && Path.IsPathRooted(path)
        ? path : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fallback);
    public static AppSettings Load(string? path = null)
    {
        path ??= SettingsPath;
        if (!File.Exists(path)) return new();
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new(); }
        catch (Exception ex) when (ex is JsonException or IOException) { throw new InvalidDataException($"Cannot read settings: {path}. {ex.Message}", ex); }
    }
    public void Save(string? path = null)
    {
        path ??= SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
