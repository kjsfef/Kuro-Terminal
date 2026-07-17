using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Kuro;

public sealed partial class ShellEngine
{
    private static readonly string[] Tips =
    [
        "Press Tab to complete commands and paths.",
        "Create a shortcut with: alias name=\"command\"",
        "Save a directory with: bookmark add projects C:\\path\\to\\projects",
        "Change the look instantly with: theme random",
        "Use 'tools list' to see the integrated open-source utilities.",
        "Use 'note add <text>' to keep quick notes inside Kuro.",
        "Unknown commands are searched in your Windows PATH.",
        "Use Ctrl+L to clear, Ctrl+U to erase input, and Ctrl+C to cancel input.",
        "Use 'palette' to view the active theme's exact colors.",
        "Use 'motd set <text>' to personalize the startup message."
    ];

    private static readonly string[] Fortunes =
    [
        "Quiet systems reveal loud truths.",
        "The cleanest command is the one you understand.",
        "Build tools that make tomorrow easier.",
        "Curiosity is powerful; permission keeps it ethical.",
        "A good terminal remembers the user, not just the command.",
        "Measure twice, delete once.",
        "Logs are stories told by machines.",
        "Make it work, make it clear, then make it yours."
    ];

    private void RegisterKuroCommands()
    {
        Register("kuro", "Kuro", "kuro", "Show the Kuro identity banner.", KuroBanner, "logo");
        Register("about", "Kuro", "about", "Show project, version, and creator information.", AboutKuro);
        Register("who-built-this", "Kuro", "who-built-this", "Show who created Kuro.", WhoBuiltThis, "builtby", "creator");
        Register("credits", "Kuro", "credits", "Show Kuro project credits.", Credits);
        Register("copyright", "Kuro", "copyright", "Show the Kuro copyright notice.", (_, _) => Ok(Copyright));
        Register("license", "Kuro", "license", "Show the included use notice.", License);
        Register("features", "Kuro", "features", "Show Kuro's main feature set.", Features);
        Register("changelog", "Kuro", "changelog", "Show the current release notes.", Changelog);
        Register("shortcuts", "Kuro", "shortcuts", "Show keyboard shortcuts.", Shortcuts);
        Register("tips", "Kuro", "tips [all]", "Show a random tip or every tip.", ShowTips, "tip");
        Register("fortune", "Kuro", "fortune", "Print a short Kuro quote.", (_, _) => Ok(Fortunes[Random.Shared.Next(Fortunes.Length)]));
        Register("motd", "Customize", "motd [show|set <text>|clear|reset]", "Customize the startup message.", MotdCommand);
        Register("title", "Customize", "title [show|set <text>|reset]", "Customize the title-bar name.", TitleCommand);
        Register("palette", "Customize", "palette", "Show the active theme's color palette.", PaletteCommand, "colors");
        Register("bookmark", "Customize", "bookmark [list|add|go|remove]", "Save and revisit directory shortcuts.", BookmarkCommand, "bm");
        Register("note", "Customize", "note [list|add|show|remove|clear]", "Store quick persistent notes.", NoteCommand, "notes");
        Register("session", "Kuro", "session", "Show details about the current Kuro session.", SessionCommand);
        Register("command-count", "Kuro", "command-count", "Show the number of built-in commands.", (_, _) => Ok($"{CommandCount} built-in commands"), "cmdcount");
        Register("identity", "Kuro", "identity", "Show the shell identity and local user.", IdentityCommand);
        Register("welcome", "Kuro", "welcome", "Print a compact welcome banner.", WelcomeCommand);
        Register("dashboard", "Kuro", "dashboard", "Show a compact live Kuro dashboard.", DashboardCommand, "dash");
        Register("profile", "Kuro", "profile", "Show your saved Kuro personalization profile.", ProfileCommand);
        Register("doctor", "Kuro", "doctor", "Check Kuro configuration and environment health.", DoctorCommand);
        Register("commandpacks", "Kuro", "commandpacks", "Show command packs and their sizes.", CommandPacksCommand, "packs");
    }

    private ShellResult KuroBanner(string[] args, string raw)
    {
        StringBuilder output = new();
        output.AppendLine("                 .      K U R O");
        output.AppendLine("             _..-'-.._  custom terminal shell");
        output.AppendLine("          .-'  .-.-.  '-.");
        output.AppendLine("         /    /     \\    \\");
        output.AppendLine("        |    |       |    |   open-source tool bridge");
        output.AppendLine("         \\    \\     /    /    version " + Version);
        output.AppendLine("          '-._ '---' _.-'");
        output.AppendLine("              '-----'");
        return Ok(output.ToString().TrimEnd());
    }

    private ShellResult AboutKuro(string[] args, string raw)
    {
        StringBuilder output = new();
        output.AppendLine("Kuro Terminal");
        output.AppendLine("-------------");
        output.AppendLine($"Version:       {Version}");
        output.AppendLine($"Commands:      {CommandCount}");
        output.AppendLine("Runtime:       Custom C# / WPF shell engine");
        output.AppendLine("Purpose:       A personal transparent terminal and command environment");
        output.AppendLine("Tool hub:       Open-source OSINT, privacy, and defensive utilities");
        output.AppendLine(Copyright);
        return Ok(output.ToString().TrimEnd());
    }

    private ShellResult WhoBuiltThis(string[] args, string raw) =>
        Ok("Kuro Terminal was designed and built by @Lthest.\n" + Copyright);

    private ShellResult Credits(string[] args, string raw) =>
        Ok("KURO CREDITS\n" +
           "  Creator / owner: @Lthest\n" +
           "  Shell engine:    Custom C# implementation\n" +
           "  Interface:       WPF transparent terminal surface\n" +
           "  Icon:            Original Kuro eclipse monogram\n" +
           "  Foundation:      Built from scratch as a personal command shell");

    private ShellResult License(string[] args, string raw) =>
        Ok("Kuro Terminal - Personal Project Use Notice\n" +
           Copyright + "\n\n" +
           "The source is included for the owner's personal development and customization. " +
           "Do not remove creator attribution, falsely claim authorship, or use Kuro's security " +
           "features against systems without explicit authorization.");

    private ShellResult Features(string[] args, string raw) =>
        Ok("Kuro features\n" +
           "  • One continuous transparent terminal surface\n" +
           "  • Persistent aliases, variables, bookmarks, notes, MOTD, prompt, and themes\n" +
           "  • File, text, system, network, developer, research, and defensive commands\n" +
           "  • Tab completion and command history\n" +
           "  • External Windows PATH command support\n" +
           "  • 25+ built-in color themes\n" +
           "  • Matching Kuro application and title-bar icon");

    private ShellResult Changelog(string[] args, string raw) =>
        Ok("Kuro 1.1.1\n" +
           "  • Rebuilt the window frame with a smoother layered glass edge\n" +
           "  • Replaced the mountain logo with the original Kuro eclipse monogram\n" +
           "  • Removed owner update and release controls from public shell help\n" +
           "  • Kept automatic update checks internal and silent\n" +
           "  • Preserved the integrated open-source tool hub");

    private ShellResult Shortcuts(string[] args, string raw) =>
        Ok("KEYBOARD SHORTCUTS\n" +
           "  Enter       execute command\n" +
           "  Tab         command/path completion\n" +
           "  Up / Down   command history\n" +
           "  Ctrl+L      clear terminal\n" +
           "  Ctrl+A      beginning of input\n" +
           "  Ctrl+E      end of input\n" +
           "  Ctrl+U      erase current input\n" +
           "  Ctrl+C      copy selection or cancel input\n" +
           "  Ctrl+V      paste into input");

    private ShellResult ShowTips(string[] args, string raw)
    {
        if (args.Length > 0 && args[0].Equals("all", StringComparison.OrdinalIgnoreCase))
            return Ok(string.Join(Environment.NewLine, Tips.Select((tip, index) => $"{index + 1,2}. {tip}")));

        return Ok(Tips[Random.Shared.Next(Tips.Length)]);
    }

    private ShellResult MotdCommand(string[] args, string raw)
    {
        string action = args.Length == 0 ? "show" : args[0].ToLowerInvariant();
        switch (action)
        {
            case "show":
                return Ok(string.IsNullOrWhiteSpace(Config.Motd) ? "MOTD is empty." : Config.Motd);
            case "set":
                if (args.Length < 2)
                    return Error("motd: use motd set <message>");
                Config.Motd = string.Join(' ', args.Skip(1));
                Config.Save();
                return Ok("MOTD saved.");
            case "clear":
                Config.Motd = string.Empty;
                Config.Save();
                return Ok("MOTD cleared.");
            case "reset":
                Config.Motd = new ShellConfig().Motd;
                Config.Save();
                return Ok("MOTD reset.");
            default:
                return Error("motd: use show, set, clear, or reset");
        }
    }

    private ShellResult TitleCommand(string[] args, string raw)
    {
        string action = args.Length == 0 ? "show" : args[0].ToLowerInvariant();
        switch (action)
        {
            case "show":
                return Ok(Config.WindowTitle);
            case "set":
                if (args.Length < 2)
                    return Error("title: use title set <text>");
                Config.WindowTitle = string.Join(' ', args.Skip(1)).Trim();
                Config.Save();
                return new ShellResult("Window title saved.", UiAction: ShellUiAction.RefreshTitle);
            case "reset":
                Config.WindowTitle = "Kuro Updated";
                Config.Save();
                return new ShellResult("Window title reset.", UiAction: ShellUiAction.RefreshTitle);
            default:
                return Error("title: use show, set, or reset");
        }
    }

    private ShellResult PaletteCommand(string[] args, string raw)
    {
        ThemePalette theme = ThemePalette.Get(Config.Theme);
        return Ok($"Theme: {theme.Name}\n" +
                  $"accent={theme.Accent}  cyan={theme.Cyan}  green={theme.Green}\n" +
                  $"yellow={theme.Yellow}  red={theme.Red}  purple={theme.Purple}\n" +
                  $"text={theme.Text}  muted={theme.Muted}\n" +
                  $"background={theme.Background}  terminal={theme.TerminalBackground}  border={theme.Border}");
    }

    private ShellResult BookmarkCommand(string[] args, string raw)
    {
        string action = args.Length == 0 ? "list" : args[0].ToLowerInvariant();
        if (action == "list")
        {
            if (Config.Bookmarks.Count == 0)
                return Ok("No bookmarks saved.");
            return Ok(string.Join(Environment.NewLine,
                Config.Bookmarks.OrderBy(item => item.Key).Select(item => $"{item.Key,-16} {item.Value}")));
        }

        if (action == "add")
        {
            if (args.Length < 2)
                return Error("bookmark: use bookmark add <name> [path]");
            string name = args[1];
            string path = args.Length >= 3 ? ResolvePath(args[2]) : CurrentDirectory;
            if (!Directory.Exists(path))
                return Error($"bookmark: directory not found: {path}");
            Config.Bookmarks[name] = path;
            Config.Save();
            return Ok($"Saved bookmark '{name}' -> {path}");
        }

        if (action is "go" or "open")
        {
            if (args.Length < 2)
                return Error("bookmark: use bookmark go <name>");
            if (!Config.Bookmarks.TryGetValue(args[1], out string? path))
                return Error($"bookmark: not found: {args[1]}");
            if (!Directory.Exists(path))
                return Error($"bookmark: saved directory no longer exists: {path}");
            SetCurrentDirectory(path);
            return Ok(CurrentDirectory);
        }

        if (action is "remove" or "delete")
        {
            if (args.Length < 2)
                return Error("bookmark: use bookmark remove <name>");
            if (!Config.Bookmarks.Remove(args[1]))
                return Error($"bookmark: not found: {args[1]}");
            Config.Save();
            return Ok($"Removed bookmark '{args[1]}'.");
        }

        if (Config.Bookmarks.TryGetValue(args[0], out string? quickPath))
        {
            if (!Directory.Exists(quickPath))
                return Error($"bookmark: saved directory no longer exists: {quickPath}");
            SetCurrentDirectory(quickPath);
            return Ok(CurrentDirectory);
        }

        return Error("bookmark: use list, add, go, or remove");
    }

    private ShellResult NoteCommand(string[] args, string raw)
    {
        string action = args.Length == 0 ? "list" : args[0].ToLowerInvariant();
        switch (action)
        {
            case "list":
                return Config.Notes.Count == 0
                    ? Ok("No notes saved.")
                    : Ok(string.Join(Environment.NewLine,
                        Config.Notes.Select((note, index) => $"{index + 1,3}. {note}")));
            case "add":
                if (args.Length < 2)
                    return Error("note: use note add <text>");
                Config.Notes.Add(string.Join(' ', args.Skip(1)));
                Config.Save();
                return Ok($"Saved note {Config.Notes.Count}.");
            case "show":
                if (args.Length < 2 || !int.TryParse(args[1], out int showIndex) ||
                    showIndex < 1 || showIndex > Config.Notes.Count)
                    return Error("note: enter a valid note number");
                return Ok(Config.Notes[showIndex - 1]);
            case "remove":
            case "delete":
                if (args.Length < 2 || !int.TryParse(args[1], out int removeIndex) ||
                    removeIndex < 1 || removeIndex > Config.Notes.Count)
                    return Error("note: enter a valid note number");
                string removed = Config.Notes[removeIndex - 1];
                Config.Notes.RemoveAt(removeIndex - 1);
                Config.Save();
                return Ok($"Removed: {removed}");
            case "clear":
                Config.Notes.Clear();
                Config.Save();
                return Ok("All notes cleared.");
            default:
                return Error("note: use list, add, show, remove, or clear");
        }
    }

    private ShellResult SessionCommand(string[] args, string raw)
    {
        using Process process = Process.GetCurrentProcess();
        TimeSpan age = DateTime.Now - process.StartTime;
        return Ok($"Kuro {Version}\n" +
                  $"User:        {Environment.UserName}\n" +
                  $"Machine:     {Environment.MachineName}\n" +
                  $"Directory:   {CurrentDirectory}\n" +
                  $"Theme:       {Config.Theme}\n" +
                  $"Opacity:     {Config.Opacity:0.00}\n" +
                  $"Font size:   {Config.FontSize:0.#}\n" +
                  $"Session age: {(int)age.TotalHours:00}:{age.Minutes:00}:{age.Seconds:00}\n" +
                  $"Memory:      {FormatSize(process.WorkingSet64)}\n" +
                  $"Built-ins:   {CommandCount}");
    }

    private ShellResult IdentityCommand(string[] args, string raw) =>
        Ok($"shell=Kuro\nversion={Version}\nbuilder={Builder}\nuser={Environment.UserName}\nhost={Environment.MachineName}\nadmin={IsAdministrator}");

    private ShellResult WelcomeCommand(string[] args, string raw) =>
        Ok($"Kuro {Version}\n{Config.Motd}");

    private ShellResult DashboardCommand(string[] args, string raw)
    {
        using Process process = Process.GetCurrentProcess();
        TimeSpan age = DateTime.Now - process.StartTime;
        string privilege = IsAdministrator ? "administrator" : "standard user";
        string opacity = Config.Opacity.ToString("0.00");
        string sessionAge = $"{(int)age.TotalHours:00}:{age.Minutes:00}:{age.Seconds:00}";
        string memory = FormatSize(process.WorkingSet64);

        StringBuilder output = new();
        output.AppendLine("╭─ KURO DASHBOARD ─────────────────────────────╮");
        output.AppendLine($"│ creator      {Builder,-33}│");
        output.AppendLine($"│ version      {Version,-33}│");
        output.AppendLine($"│ built-ins    {CommandCount,-33}│");
        output.AppendLine($"│ theme        {Config.Theme,-33}│");
        output.AppendLine($"│ opacity      {opacity,-33}│");
        output.AppendLine($"│ user         {Environment.UserName,-33}│");
        output.AppendLine($"│ privilege    {privilege,-33}│");
        output.AppendLine($"│ session      {sessionAge,-33}│");
        output.AppendLine($"│ memory       {memory,-33}│");
        output.Append("╰──────────────────────────────────────────────╯");
        return Ok(output.ToString());
    }

    private ShellResult ProfileCommand(string[] args, string raw) =>
        Ok($"Kuro profile\n" +
           $"  creator:    {Builder}\n" +
           $"  title:      {Config.WindowTitle}\n" +
           $"  theme:      {Config.Theme}\n" +
           $"  opacity:    {Config.Opacity:0.00}\n" +
           $"  font:       {Config.FontSize:0.#}\n" +
           $"  prompt:     {Config.PromptFormat}\n" +
           $"  MOTD:       {Config.Motd}\n" +
           $"  aliases:    {Config.Aliases.Count}\n" +
           $"  variables:  {Config.Variables.Count}\n" +
           $"  bookmarks:  {Config.Bookmarks.Count}\n" +
           $"  notes:      {Config.Notes.Count}\n" +
           $"  config:     {ShellConfig.ConfigPath}");

    private ShellResult DoctorCommand(string[] args, string raw)
    {
        List<string> checks = new();
        checks.Add($"[ok] current directory: {CurrentDirectory}");
        checks.Add(Directory.Exists(CurrentDirectory)
            ? "[ok] current directory exists"
            : "[!!] current directory is missing");
        checks.Add(IsDirectoryWritable(CurrentDirectory)
            ? "[ok] current directory is writable"
            : "[--] current directory is read-only or protected");
        checks.Add($"[ok] config path: {ShellConfig.ConfigPath}");
        checks.Add($"[ok] PATH entries: {(Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Length}");
        checks.Add($"[ok] loaded themes: {ThemePalette.Names.Count}");
        checks.Add($"[ok] loaded commands: {CommandCount}");
        checks.Add(IsAdministrator
            ? "[info] Kuro is running elevated"
            : "[info] Kuro is running as a standard user (recommended)");
        return Ok("KURO DOCTOR\n" + string.Join(Environment.NewLine, checks));
    }

    private bool IsDirectoryWritable(string directory)
    {
        string testPath = Path.Combine(directory, $".kuro-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            using (File.Create(testPath)) { }
            File.Delete(testPath);
            return true;
        }
        catch
        {
            try { if (File.Exists(testPath)) File.Delete(testPath); } catch { }
            return false;
        }
    }

    private ShellResult CommandPacksCommand(string[] args, string raw)
    {
        IEnumerable<IGrouping<string, CommandDefinition>> groups = Commands
            .GroupBy(command => command.Category)
            .OrderBy(group => group.Key);
        StringBuilder output = new();
        output.AppendLine("KURO COMMAND PACKS");
        foreach (IGrouping<string, CommandDefinition> group in groups)
            output.AppendLine($"  {group.Key,-16} {group.Count(),3} commands");
        const string totalLabel = "TOTAL";
        output.AppendLine($"  {totalLabel,-16} {CommandCount,3} commands");
        return Ok(output.ToString().TrimEnd());
    }
}
