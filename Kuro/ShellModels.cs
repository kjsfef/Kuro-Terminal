using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Kuro;

public enum ShellUiAction
{
    None,
    ApplyAppearance,
    RefreshTitle
}

public readonly record struct ShellResult(
    string Output,
    bool IsError = false,
    bool ClearScreen = false,
    bool ExitRequested = false,
    bool ClearHistory = false,
    ShellUiAction UiAction = ShellUiAction.None)
{
    public static ShellResult Empty => new(string.Empty);
}

public readonly record struct CompletionResult(
    string CompletedInput,
    IReadOnlyList<string> Matches);

public sealed class ShellConfig
{
    public int ConfigVersion { get; set; } = 3;
    public string Theme { get; set; } = "kuro";
    public double Opacity { get; set; } = 0.64;
    public double FontSize { get; set; } = 15;
    public bool ShowGreeting { get; set; } = true;
    public bool AutoUpdate { get; set; } = true;
    public string PromptFormat { get; set; } = "[{user}@{host} {path}]{symbol} ";
    public string Motd { get; set; } = "night mode engaged // type 'help' to explore Kuro";
    public string WindowTitle { get; set; } = "Kuro Updated";

    public Dictionary<string, string> Aliases { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ll"] = "ls -la",
            ["la"] = "ls -a",
            [".."] = "cd ..",
            ["..."] = "cd ../..",
            ["home"] = "cd ~",
            ["cls"] = "clear",
            ["creator"] = "who-built-this",
            ["sec"] = "security"
        };

    public Dictionary<string, string> Variables { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Bookmarks { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ["desktop"] = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            ["documents"] = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            ["downloads"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        };

    public List<string> Notes { get; set; } = new();

    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kuro");

    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    private static string LegacyConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArchShell", "config.json");

    public static ShellConfig Load()
    {
        try
        {
            string? path = File.Exists(ConfigPath)
                ? ConfigPath
                : File.Exists(LegacyConfigPath)
                    ? LegacyConfigPath
                    : null;

            if (path is null)
                return new ShellConfig();

            ShellConfig? loaded = JsonSerializer.Deserialize<ShellConfig>(File.ReadAllText(path));
            if (loaded is null)
                return new ShellConfig();

            bool needsVisualUpgrade = loaded.ConfigVersion < 2;
            bool needsUpdateUpgrade = loaded.ConfigVersion < 3;
            loaded.Normalize();

            if (needsVisualUpgrade)
            {
                loaded.Opacity = Math.Min(loaded.Opacity, 0.64);
                loaded.ConfigVersion = 3;
            }

            if (string.Equals(path, LegacyConfigPath, StringComparison.OrdinalIgnoreCase))
            {
                if (loaded.Theme.Equals("arch", StringComparison.OrdinalIgnoreCase))
                    loaded.Theme = "kuro";
                loaded.WindowTitle = "kuro";
                loaded.Opacity = Math.Min(loaded.Opacity, 0.64);
                loaded.Save();
            }
            else if (needsVisualUpgrade || needsUpdateUpgrade)
            {
                loaded.ConfigVersion = 3;
                loaded.Save();
            }

            return loaded;
        }
        catch
        {
            return new ShellConfig();
        }
    }

    public void Save()
    {
        Normalize();
        Directory.CreateDirectory(ConfigDirectory);
        JsonSerializerOptions options = new() { WriteIndented = true };
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, options));
    }

    public void Reset()
    {
        ShellConfig defaults = new();
        ConfigVersion = defaults.ConfigVersion;
        Theme = defaults.Theme;
        Opacity = defaults.Opacity;
        FontSize = defaults.FontSize;
        ShowGreeting = defaults.ShowGreeting;
        AutoUpdate = defaults.AutoUpdate;
        PromptFormat = defaults.PromptFormat;
        Motd = defaults.Motd;
        WindowTitle = defaults.WindowTitle;
        Aliases = defaults.Aliases;
        Variables = defaults.Variables;
        Bookmarks = defaults.Bookmarks;
        Notes = defaults.Notes;
        Save();
    }

    private void Normalize()
    {
        ConfigVersion = Math.Max(ConfigVersion, 3);
        Aliases = new Dictionary<string, string>(
            Aliases ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
        Variables = new Dictionary<string, string>(
            Variables ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
        Bookmarks = new Dictionary<string, string>(
            Bookmarks ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
        Notes ??= new List<string>();

        Theme = string.IsNullOrWhiteSpace(Theme) ? "kuro" : Theme;
        if (!ThemePalette.Names.Contains(Theme, StringComparer.OrdinalIgnoreCase))
            Theme = "kuro";

        PromptFormat = string.IsNullOrEmpty(PromptFormat)
            ? "[{user}@{host} {path}]{symbol} "
            : PromptFormat;
        Motd ??= string.Empty;
        WindowTitle = string.IsNullOrWhiteSpace(WindowTitle) ? "kuro" : WindowTitle.Trim();
        Opacity = Math.Clamp(Opacity, 0.20, 0.96);
        FontSize = Math.Clamp(FontSize, 10, 30);
    }
}

public sealed record ThemePalette(
    string Name,
    string Accent,
    string Cyan,
    string Green,
    string Yellow,
    string Red,
    string Purple,
    string Text,
    string Muted,
    string Background,
    string TitleBackground,
    string TerminalBackground,
    string Border)
{
    private static readonly Dictionary<string, ThemePalette> Themes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["kuro"] = new(
                "kuro", "#9B7BFF", "#58DFFF", "#8BE8B2", "#F2D58A",
                "#FF6B86", "#C777FF", "#E8E7F2", "#868499", "#080810",
                "#10101A", "#06060C", "#7456C8"),
            ["moonfall"] = new(
                "moonfall", "#8E9CFF", "#73E2FF", "#9BF4D5", "#FFE6A7",
                "#FF7A9A", "#C8A0FF", "#F0F1FF", "#858AA8", "#080B18",
                "#10152A", "#060914", "#6775D8"),
            ["void"] = new(
                "void", "#8A72FF", "#5ED8FF", "#77E8A6", "#EECF75",
                "#FF5D7D", "#B879FF", "#E9E7F3", "#777386", "#030307",
                "#090910", "#020205", "#54429D"),
            ["sakura"] = new(
                "sakura", "#FF8FB8", "#8DDCF7", "#9EE6BE", "#FFD69A",
                "#FF667E", "#D6A0FF", "#FFF0F6", "#A78595", "#1A0C15",
                "#28131F", "#120810", "#DB6E9D"),
            ["neon"] = new(
                "neon", "#B6FF00", "#00F5FF", "#55FF9A", "#FFF35C",
                "#FF477E", "#D05CFF", "#F0FFE9", "#77A487", "#050A0A",
                "#091314", "#030707", "#59D900"),
            ["synthwave"] = new(
                "synthwave", "#FF5CD9", "#4DEEFF", "#72F1B8", "#F9F871",
                "#FF5470", "#A47CFF", "#FCEBFF", "#9A77A2", "#120A20",
                "#201036", "#0C0615", "#E64BC3"),
            ["nordic"] = new(
                "nordic", "#88C0D0", "#8FBCBB", "#A3BE8C", "#EBCB8B",
                "#BF616A", "#B48EAD", "#ECEFF4", "#7F8B9A", "#111820",
                "#18232E", "#0D1319", "#5E81AC"),
            ["solar"] = new(
                "solar", "#268BD2", "#2AA198", "#859900", "#B58900",
                "#DC322F", "#6C71C4", "#EEE8D5", "#839496", "#002B36",
                "#073642", "#00242D", "#268BD2"),
            ["mocha"] = new(
                "mocha", "#CBA6F7", "#89DCEB", "#A6E3A1", "#F9E2AF",
                "#F38BA8", "#F5C2E7", "#CDD6F4", "#7F849C", "#11111B",
                "#181825", "#0D0D15", "#B4BEFE"),
            ["gruv"] = new(
                "gruv", "#FABD2F", "#83A598", "#B8BB26", "#D79921",
                "#FB4934", "#D3869B", "#EBDBB2", "#928374", "#1D2021",
                "#282828", "#17191A", "#D65D0E"),
            ["cyber"] = new(
                "cyber", "#00E5FF", "#53F6FF", "#00FF9C", "#FFE600",
                "#FF3B69", "#D85CFF", "#E7FCFF", "#57818A", "#020B10",
                "#04161E", "#01070A", "#00A8C8"),
            ["ocean"] = new(
                "ocean", "#4CB8FF", "#4FE3D8", "#7BE0A3", "#F5D67B",
                "#FF7285", "#A88CFF", "#E6F7FF", "#7491A1", "#06131D",
                "#0A2030", "#040D14", "#2388C9"),
            ["ember"] = new(
                "ember", "#FF8A3D", "#FFB86B", "#B9D96B", "#FFD166",
                "#FF5A5F", "#D68BFF", "#FFF0DB", "#9D7D68", "#170906",
                "#26100A", "#100604", "#E05D24"),
            ["frost"] = new(
                "frost", "#77C8FF", "#A0F0FF", "#91E6C3", "#F4E3A1",
                "#FF8194", "#C1ABFF", "#F2FBFF", "#849CA8", "#07141C",
                "#0D202A", "#050E13", "#77C8FF"),
            ["venom"] = new(
                "venom", "#7DFF57", "#4FFFD7", "#91FF70", "#E5FF66",
                "#FF5F68", "#B55CFF", "#E9FFE4", "#67866C", "#041006",
                "#081A0B", "#030B04", "#48C936"),
            ["crimson"] = new(
                "crimson", "#FF4664", "#6DD9F2", "#7EDB9C", "#F5C96B",
                "#FF2D55", "#D66BFF", "#FFECEF", "#9B737A", "#160609",
                "#240A10", "#100406", "#D72D4C"),
            ["aurora"] = new(
                "aurora", "#67E8F9", "#22D3EE", "#86EFAC", "#FDE68A",
                "#FB7185", "#C084FC", "#F0FDFA", "#78979A", "#061212",
                "#0A2020", "#040C0C", "#43C6D4"),
            ["ghost"] = new(
                "ghost", "#D6D6E7", "#BFD7EA", "#C8E6C9", "#F1E3B4",
                "#F29CA3", "#D4C1EC", "#F7F7FA", "#9292A0", "#101014",
                "#18181E", "#0B0B0E", "#A6A6B8"),
            ["amethyst"] = new(
                "amethyst", "#B886FF", "#74D7FF", "#8BE0AE", "#EED88B",
                "#FF708B", "#E3A6FF", "#F4ECFF", "#9580A5", "#120A1C",
                "#1D102B", "#0C0613", "#9457D6"),
            ["classic"] = new(
                "classic", "#C0C0C0", "#A7D8DE", "#B4D6A0", "#E8D7A0",
                "#E88484", "#C9A7DE", "#F0F0F0", "#8A8A8A", "#0C0C0C",
                "#161616", "#080808", "#6F6F6F"),
            ["arch"] = new(
                "arch", "#61AFEF", "#56B6C2", "#98C379", "#E5C07B",
                "#E06C75", "#C678DD", "#D8DEE9", "#7F8C9D", "#0A0E13",
                "#101820", "#070B10", "#2D96D2"),
            ["dracula"] = new(
                "dracula", "#BD93F9", "#8BE9FD", "#50FA7B", "#F1FA8C",
                "#FF5555", "#FF79C6", "#F8F8F2", "#8B8CA7", "#171821",
                "#222330", "#13141B", "#BD93F9"),
            ["matrix"] = new(
                "matrix", "#39FF88", "#00E5A0", "#63FF8B", "#D7FF52",
                "#FF5A5F", "#88FFB7", "#D7FFE5", "#66A979", "#031008",
                "#061A0D", "#020B05", "#1DCE69"),
            ["amber"] = new(
                "amber", "#FFB000", "#FFD166", "#B7D36B", "#FFE08A",
                "#FF6B4A", "#E6A8FF", "#FFE7B0", "#A88B58", "#160E03",
                "#211607", "#100A02", "#C77F00"),
            ["ice"] = new(
                "ice", "#65C7F7", "#88E0EF", "#8CE6C0", "#F3D98B",
                "#FF7D90", "#B9A2FF", "#EAF7FF", "#8098A8", "#07121A",
                "#0C1C27", "#050D13", "#65C7F7"),
            ["mono"] = new(
                "mono", "#F1F1F1", "#D8D8D8", "#E5E5E5", "#FFFFFF",
                "#FF7777", "#CCCCCC", "#F4F4F4", "#8A8A8A", "#111111",
                "#1B1B1B", "#0B0B0B", "#777777")
        };

    public static IReadOnlyCollection<string> Names => Themes.Keys.OrderBy(name => name).ToArray();

    public static ThemePalette Get(string name) =>
        Themes.TryGetValue(name, out ThemePalette? palette) ? palette : Themes["kuro"];

    public static string GetRandomName() =>
        Names.ElementAt(Random.Shared.Next(Names.Count));
}
