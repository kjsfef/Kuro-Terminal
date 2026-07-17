using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace Kuro;

public sealed partial class ShellEngine
{
    public static string Version
    {
        get
        {
            string? informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
                return informational.Split('+')[0];
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0";
        }
    }
    public const string Builder = "@Lthest";
    public const string Copyright = "Copyright © 2026 @Lthest. All rights reserved.";

    private readonly Dictionary<string, CommandDefinition> _commands =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<string> _directoryStack = new();
    private string _previousDirectory;

    public ShellEngine()
    {
        Config = ShellConfig.Load();
        CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _previousDirectory = CurrentDirectory;
        RegisterCommands();
    }

    public ShellConfig Config { get; }

    public string CurrentDirectory { get; private set; }

    public string UserName => Environment.UserName.ToLowerInvariant();

    public string HostName => Environment.MachineName.ToLowerInvariant();

    public bool IsAdministrator
    {
        get
        {
            try
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    public string DisplayPath
    {
        get
        {
            string home = HomeDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string current = CurrentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(current, home, StringComparison.OrdinalIgnoreCase))
                return "~";

            if (current.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                string relative = current[(home.Length + 1)..]
                    .Replace(Path.DirectorySeparatorChar, '/');
                return $"~/{relative}";
            }

            string root = Path.GetPathRoot(current) ?? string.Empty;
            if (string.Equals(root.TrimEnd(Path.DirectorySeparatorChar), current,
                    StringComparison.OrdinalIgnoreCase))
                return current;

            string folder = Path.GetFileName(current);
            return string.IsNullOrWhiteSpace(folder) ? current : folder;
        }
    }

    public string HomeDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string PromptText => ExpandPrompt(Config.PromptFormat);

    public int CommandCount => Commands.Count;

    public IReadOnlyCollection<CommandDefinition> Commands =>
        _commands.Values
            .DistinctBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Name)
            .ToArray();

    public ShellResult Execute(string input, IReadOnlyList<string> history)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ShellResult.Empty;

        try
        {
            string expanded = ExpandAliases(input.Trim());
            expanded = ExpandVariables(expanded);
            List<string> tokens = Tokenize(expanded);
            if (tokens.Count == 0)
                return ShellResult.Empty;

            string commandName = tokens[0];
            string[] args = tokens.Skip(1).ToArray();
            string rawArguments = GetRawArguments(expanded);

            if (_commands.TryGetValue(commandName, out CommandDefinition? command))
                return command.Handler(args, rawArguments);

            return RunExternal(tokens.ToArray(), false);
        }
        catch (UnauthorizedAccessException)
        {
            return Error("Permission denied.");
        }
        catch (IOException ex)
        {
            return Error($"I/O error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Error($"Error: {ex.Message}");
        }
    }

    public ShellResult ExecuteHistoryCommand(string input, IReadOnlyList<string> history)
    {
        List<string> tokens = Tokenize(input);
        if (tokens.Count == 0 || !tokens[0].Equals("history", StringComparison.OrdinalIgnoreCase))
            return Execute(input, history);

        if (tokens.Count > 1 && tokens[1].Equals("-c", StringComparison.OrdinalIgnoreCase))
            return new ShellResult("History cleared.", ClearHistory: true);

        if (tokens.Count > 1 && int.TryParse(tokens[1], out int count))
        {
            count = Math.Clamp(count, 1, history.Count);
            IEnumerable<string> selected = history.Skip(Math.Max(0, history.Count - count));
            int start = Math.Max(1, history.Count - count + 1);
            return Ok(string.Join(Environment.NewLine,
                selected.Select((item, index) => $"{start + index,4}  {item}")));
        }

        return history.Count == 0
            ? Ok("History is empty.")
            : Ok(string.Join(Environment.NewLine,
                history.Select((item, index) => $"{index + 1,4}  {item}")));
    }

    public CompletionResult Complete(string input)
    {
        string beforeCaret = input;
        int tokenStart = FindCurrentTokenStart(beforeCaret);
        string prefix = beforeCaret[tokenStart..].TrimStart('"');
        bool firstToken = string.IsNullOrWhiteSpace(beforeCaret[..tokenStart]);

        List<string> matches = firstToken
            ? GetCommandCompletions(prefix)
            : GetPathCompletions(prefix);

        if (matches.Count == 0)
            return new CompletionResult(input, Array.Empty<string>());

        string replacement;
        if (matches.Count == 1)
        {
            replacement = matches[0];
            if (!firstToken && replacement.Contains(' ') && !replacement.StartsWith('"'))
                replacement = $"\"{replacement}\"";

            if (!firstToken && Directory.Exists(ResolvePathSafe(replacement.Trim('"'))))
                replacement += Path.DirectorySeparatorChar;
            else if (firstToken)
                replacement += " ";
        }
        else
        {
            replacement = LongestCommonPrefix(matches);
            if (string.IsNullOrEmpty(replacement) || replacement.Length <= prefix.Length)
                return new CompletionResult(input, matches);
        }

        string completed = beforeCaret[..tokenStart] + replacement;
        return new CompletionResult(completed, matches);
    }

    public string ExpandPrompt(string format)
    {
        string symbol = IsAdministrator ? "#" : "$";
        return format
            .Replace("{user}", UserName, StringComparison.OrdinalIgnoreCase)
            .Replace("{host}", "kuro", StringComparison.OrdinalIgnoreCase)
            .Replace("{machine}", HostName, StringComparison.OrdinalIgnoreCase)
            .Replace("{path}", DisplayPath, StringComparison.OrdinalIgnoreCase)
            .Replace("{fullpath}", CurrentDirectory, StringComparison.OrdinalIgnoreCase)
            .Replace("{symbol}", symbol, StringComparison.OrdinalIgnoreCase);
    }

    private void RegisterCommands()
    {
        RegisterCoreCommands();
        RegisterFileCommands();
        RegisterSystemCommands();
        RegisterUtilityCommands();
        RegisterKuroCommands();
        RegisterExtraFileCommands();
        RegisterDeveloperCommands();
        RegisterSecurityCommands();
        RegisterOpenSourceToolCommands();
        RegisterExtraSystemCommands();
    }

    private void Register(
        string name,
        string category,
        string usage,
        string description,
        Func<string[], string, ShellResult> handler,
        params string[] aliases)
    {
        CommandDefinition definition = new(name, category, usage, description, handler);
        _commands[name] = definition;
        foreach (string alias in aliases)
            _commands[alias] = definition;
    }

    private void RegisterCoreCommands()
    {
        Register("help", "Shell", "help [command]", "Show all commands or detailed help.", HelpCommand, "man");
        Register("commands", "Shell", "commands", "Print every built-in command name.", CommandsCommand);
        Register("categories", "Shell", "categories", "List command categories and command counts.", CategoriesCommand);
        Register("clear", "Shell", "clear", "Clear the terminal screen.", (_, _) => new ShellResult(string.Empty, ClearScreen: true), "cls");
        Register("exit", "Shell", "exit", "Close Kuro.", (_, _) => new ShellResult("Goodbye.", ExitRequested: true), "quit");
        Register("version", "Shell", "version", "Show the Kuro version.", (_, _) => Ok($"Kuro {Version}"), "ver");
        Register("history", "Shell", "history [count|-c]", "Show or clear command history.", (_, _) => Ok("History is handled by the terminal window."));
        Register("alias", "Shell", "alias [name=command]", "Create or list persistent command aliases.", AliasCommand);
        Register("unalias", "Shell", "unalias <name>", "Remove a saved alias.", UnaliasCommand);
        Register("which", "Shell", "which <command>", "Locate a built-in, alias, or executable.", WhichCommand, "where");
        Register("env", "Shell", "env [--all]", "List Kuro variables or all environment variables.", EnvCommand);
        Register("set", "Shell", "set NAME=value", "Create a persistent Kuro variable.", SetVariableCommand, "export");
        Register("unset", "Shell", "unset <NAME>", "Remove an Kuro variable.", UnsetVariableCommand);
        Register("prompt", "Customize", "prompt [show|set <format>|reset]", "Customize the saved prompt format.", PromptCommand);
        Register("theme", "Customize", "theme [name|list]", "Change the saved color theme.", ThemeCommand);
        Register("opacity", "Customize", "opacity [0.20-0.96]", "Change terminal background transparency.", OpacityCommand, "transparent");
        Register("font", "Customize", "font [10-30]", "Change the terminal font size.", FontCommand);
        Register("greeting", "Customize", "greeting [on|off]", "Show or hide the startup greeting.", GreetingCommand);
        Register("config", "Customize", "config [show|path|reset]", "Inspect or reset your saved settings.", ConfigCommand);
    }

    private ShellResult HelpCommand(string[] args, string raw)
    {
        if (args.Length > 0)
        {
            if (args[0].Equals("categories", StringComparison.OrdinalIgnoreCase))
                return CategoriesCommand(Array.Empty<string>(), string.Empty);

            if (args[0].Equals("category", StringComparison.OrdinalIgnoreCase) && args.Length > 1)
            {
                string requestedCategory = string.Join(' ', args.Skip(1));
                CommandDefinition[] matches = Commands
                    .Where(command => command.Category.Equals(requestedCategory, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (matches.Length == 0)
                    return Error($"help: unknown category: {requestedCategory}");

                StringBuilder categoryOutput = new();
                categoryOutput.AppendLine(requestedCategory.ToUpperInvariant());
                categoryOutput.AppendLine(new string('─', 58));
                foreach (CommandDefinition match in matches)
                    categoryOutput.AppendLine($"  {match.Usage,-38} {match.Description}");
                return Ok(categoryOutput.ToString().TrimEnd());
            }

            string name = args[0];
            if (_commands.TryGetValue(name, out CommandDefinition? command))
                return Ok($"{command.Name}\n  category: {command.Category}\n  usage:    {command.Usage}\n  {command.Description}");

            return Error($"help: unknown command: {name}");
        }

        StringBuilder output = new();
        output.AppendLine($"Kuro {Version} built-in commands");
        output.AppendLine(new string('─', 58));

        foreach (IGrouping<string, CommandDefinition> group in Commands.GroupBy(c => c.Category))
        {
            output.AppendLine();
            output.AppendLine(group.Key.ToUpperInvariant());
            foreach (CommandDefinition command in group)
                output.AppendLine($"  {command.Usage,-34} {command.Description}");
        }

        output.AppendLine();
        output.AppendLine("Unknown commands are searched in your Windows PATH and run directly.");
        output.AppendLine("Use quotes around paths containing spaces. Press Tab for completion.");
        output.AppendLine("Use 'categories' or 'help category <name>' to browse smaller groups.");
        return Ok(output.ToString().TrimEnd());
    }

    private ShellResult CommandsCommand(string[] args, string raw) =>
        Ok(string.Join("  ", Commands.Select(c => c.Name)));

    private ShellResult CategoriesCommand(string[] args, string raw) =>
        Ok(string.Join(Environment.NewLine, Commands
            .GroupBy(command => command.Category)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key,-18} {group.Count(),3} commands")) +
           "\n\nUse: help category <name>");

    private ShellResult AliasCommand(string[] args, string raw)
    {
        if (args.Length == 0)
        {
            if (Config.Aliases.Count == 0)
                return Ok("No aliases are configured.");

            return Ok(string.Join(Environment.NewLine,
                Config.Aliases.OrderBy(p => p.Key)
                    .Select(p => $"alias {p.Key}='{p.Value}'")));
        }

        string joined = string.Join(' ', args);
        int equalsIndex = joined.IndexOf('=');
        string name;
        string value;

        if (equalsIndex >= 1)
        {
            name = joined[..equalsIndex].Trim();
            value = joined[(equalsIndex + 1)..].Trim().Trim('"', '\'');
        }
        else if (args.Length >= 2)
        {
            name = args[0];
            value = string.Join(' ', args.Skip(1));
        }
        else
        {
            return Config.Aliases.TryGetValue(args[0], out string? existing)
                ? Ok($"alias {args[0]}='{existing}'")
                : Error($"alias: not found: {args[0]}");
        }

        if (!Regex.IsMatch(name, "^[A-Za-z0-9_.-]+$"))
            return Error("alias: names may contain letters, numbers, dots, underscores, and dashes.");
        if (string.IsNullOrWhiteSpace(value))
            return Error("alias: command cannot be empty.");

        Config.Aliases[name] = value;
        Config.Save();
        return Ok($"Saved alias: {name} -> {value}");
    }

    private ShellResult UnaliasCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("unalias: missing alias name");

        if (!Config.Aliases.Remove(args[0]))
            return Error($"unalias: not found: {args[0]}");

        Config.Save();
        return Ok($"Removed alias: {args[0]}");
    }

    private ShellResult WhichCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("which: missing command name");

        string name = args[0];
        if (Config.Aliases.TryGetValue(name, out string? alias))
            return Ok($"{name}: alias for '{alias}'");
        if (_commands.TryGetValue(name, out CommandDefinition? command))
            return Ok($"{name}: Kuro built-in ({command.Name})");

        string? executable = FindExecutable(name);
        return executable is null
            ? Error($"which: command not found: {name}")
            : Ok(executable);
    }

    private ShellResult EnvCommand(string[] args, string raw)
    {
        IEnumerable<KeyValuePair<string, string>> values;
        if (args.Contains("--all", StringComparer.OrdinalIgnoreCase))
        {
            values = Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .Select(e => new KeyValuePair<string, string>(e.Key?.ToString() ?? string.Empty, e.Value?.ToString() ?? string.Empty))
                .OrderBy(p => p.Key);
        }
        else
        {
            values = Config.Variables.OrderBy(p => p.Key);
        }

        string result = string.Join(Environment.NewLine, values.Select(p => $"{p.Key}={p.Value}"));
        return Ok(string.IsNullOrEmpty(result) ? "No custom variables. Use: set NAME=value" : result);
    }

    private ShellResult SetVariableCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("set: use set NAME=value");

        string joined = string.Join(' ', args);
        int separator = joined.IndexOf('=');
        if (separator <= 0)
            return Error("set: use set NAME=value");

        string name = joined[..separator].Trim();
        string value = joined[(separator + 1)..].Trim();
        if (!Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$"))
            return Error("set: invalid variable name");

        Config.Variables[name] = value;
        Config.Save();
        return Ok($"{name}={value}");
    }

    private ShellResult UnsetVariableCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("unset: missing variable name");

        if (!Config.Variables.Remove(args[0]))
            return Error($"unset: variable not found: {args[0]}");

        Config.Save();
        return ShellResult.Empty;
    }

    private ShellResult PromptCommand(string[] args, string raw)
    {
        if (args.Length == 0 || args[0].Equals("show", StringComparison.OrdinalIgnoreCase))
        {
            return Ok($"Prompt format: {Config.PromptFormat}\nTokens: {{user}} {{host}} {{machine}} {{path}} {{fullpath}} {{symbol}}");
        }

        if (args[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            Config.PromptFormat = "[{user}@{host} {path}]{symbol} ";
            Config.Save();
            return Ok("Prompt reset.");
        }

        if (!args[0].Equals("set", StringComparison.OrdinalIgnoreCase) || args.Length < 2)
            return Error("prompt: use prompt set \"[{user}@{host} {path}]{symbol} \"");

        Config.PromptFormat = string.Join(' ', args.Skip(1));
        if (!Config.PromptFormat.EndsWith(' '))
            Config.PromptFormat += " ";
        Config.Save();
        return Ok($"Prompt changed to: {PromptText}");
    }

    private ShellResult ThemeCommand(string[] args, string raw)
    {
        if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            return Ok($"Themes ({ThemePalette.Names.Count}): {string.Join(", ", ThemePalette.Names)}\nCurrent: {Config.Theme}\nUse 'theme random' for a surprise.");

        string requested = args[0].Equals("random", StringComparison.OrdinalIgnoreCase)
            ? ThemePalette.GetRandomName()
            : args[0].ToLowerInvariant();
        if (!ThemePalette.Names.Contains(requested, StringComparer.OrdinalIgnoreCase))
            return Error($"theme: unknown theme '{requested}'. Use: theme list");

        Config.Theme = requested;
        Config.Save();
        return new ShellResult($"Theme changed to {requested}.", UiAction: ShellUiAction.ApplyAppearance);
    }

    private ShellResult OpacityCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Ok($"Opacity: {Config.Opacity:0.00}");

        if (!double.TryParse(args[0], out double opacity))
            return Error("opacity: enter a number from 0.20 to 0.96");

        opacity = Math.Clamp(opacity, 0.20, 0.96);
        Config.Opacity = opacity;
        Config.Save();
        return new ShellResult($"Opacity changed to {opacity:0.00}.", UiAction: ShellUiAction.ApplyAppearance);
    }

    private ShellResult FontCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Ok($"Font size: {Config.FontSize:0.#}");

        if (!double.TryParse(args[0], out double size))
            return Error("font: enter a number from 10 to 30");

        size = Math.Clamp(size, 10, 30);
        Config.FontSize = size;
        Config.Save();
        return new ShellResult($"Font size changed to {size:0.#}.", UiAction: ShellUiAction.ApplyAppearance);
    }

    private ShellResult GreetingCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Ok($"Greeting: {(Config.ShowGreeting ? "on" : "off")}");

        if (args[0].Equals("on", StringComparison.OrdinalIgnoreCase))
            Config.ShowGreeting = true;
        else if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
            Config.ShowGreeting = false;
        else
            return Error("greeting: use greeting on or greeting off");

        Config.Save();
        return Ok($"Greeting turned {(Config.ShowGreeting ? "on" : "off")}.");
    }

    private ShellResult ConfigCommand(string[] args, string raw)
    {
        string action = args.Length == 0 ? "show" : args[0].ToLowerInvariant();
        return action switch
        {
            "show" => Ok($"theme={Config.Theme}\nopacity={Config.Opacity:0.00}\nfont={Config.FontSize:0.#}\ngreeting={Config.ShowGreeting}\nprompt={Config.PromptFormat}\ntitle={Config.WindowTitle}\nmotd={Config.Motd}\naliases={Config.Aliases.Count}\nvariables={Config.Variables.Count}\nbookmarks={Config.Bookmarks.Count}\nnotes={Config.Notes.Count}"),
            "path" => Ok(ShellConfig.ConfigPath),
            "reset" => ResetConfig(),
            _ => Error("config: use config show, config path, or config reset")
        };
    }

    private ShellResult ResetConfig()
    {
        Config.Reset();
        return new ShellResult("Configuration reset to defaults.", UiAction: ShellUiAction.ApplyAppearance);
    }

    private string ExpandAliases(string input)
    {
        string expanded = input;
        for (int iteration = 0; iteration < 10; iteration++)
        {
            List<string> tokens = Tokenize(expanded);
            if (tokens.Count == 0 || !Config.Aliases.TryGetValue(tokens[0], out string? replacement))
                return expanded;

            string remainder = GetRawArguments(expanded);
            expanded = string.IsNullOrWhiteSpace(remainder)
                ? replacement
                : replacement + " " + remainder;
        }

        throw new InvalidOperationException("Alias expansion loop detected.");
    }

    private string ExpandVariables(string input)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase)
        {
            ["HOME"] = HomeDirectory,
            ["USER"] = Environment.UserName,
            ["HOST"] = HostName,
            ["PWD"] = CurrentDirectory
        };

        foreach ((string key, string value) in Config.Variables)
            values[key] = value;

        string result = Regex.Replace(input, @"\$\{([A-Za-z_][A-Za-z0-9_]*)\}|\$([A-Za-z_][A-Za-z0-9_]*)",
            match =>
            {
                string name = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (values.TryGetValue(name, out string? value))
                    return value;
                return Environment.GetEnvironmentVariable(name) ?? match.Value;
            });

        return Environment.ExpandEnvironmentVariables(result);
    }

    private List<string> GetCommandCompletions(string prefix)
    {
        return _commands.Keys
            .Concat(Config.Aliases.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name)
            .ToList();
    }

    private List<string> GetPathCompletions(string prefix)
    {
        try
        {
            string normalized = prefix.Replace('/', Path.DirectorySeparatorChar);
            string directoryPart = Path.GetDirectoryName(normalized) ?? string.Empty;
            string namePart = Path.GetFileName(normalized);
            string searchDirectory = string.IsNullOrEmpty(directoryPart)
                ? CurrentDirectory
                : ResolvePath(directoryPart);

            if (!Directory.Exists(searchDirectory))
                return new List<string>();

            return Directory.GetFileSystemEntries(searchDirectory)
                .Where(path => Path.GetFileName(path).StartsWith(namePart, StringComparison.OrdinalIgnoreCase))
                .Select(path => string.IsNullOrEmpty(directoryPart)
                    ? Path.GetFileName(path)
                    : Path.Combine(directoryPart, Path.GetFileName(path)))
                .OrderBy(path => path)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static int FindCurrentTokenStart(string input)
    {
        bool inQuotes = false;
        int start = 0;
        for (int index = 0; index < input.Length; index++)
        {
            if (input[index] == '"')
                inQuotes = !inQuotes;
            else if (char.IsWhiteSpace(input[index]) && !inQuotes)
                start = index + 1;
        }
        return start;
    }

    private static string LongestCommonPrefix(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return string.Empty;

        string prefix = values[0];
        foreach (string value in values.Skip(1))
        {
            int length = 0;
            while (length < prefix.Length && length < value.Length &&
                   char.ToUpperInvariant(prefix[length]) == char.ToUpperInvariant(value[length]))
                length++;
            prefix = prefix[..length];
            if (prefix.Length == 0)
                break;
        }
        return prefix;
    }

    private static string GetRawArguments(string input)
    {
        bool inQuotes = false;
        for (int index = 0; index < input.Length; index++)
        {
            if (input[index] == '"')
                inQuotes = !inQuotes;
            else if (char.IsWhiteSpace(input[index]) && !inQuotes)
                return input[(index + 1)..].TrimStart();
        }
        return string.Empty;
    }

    internal string ResolvePath(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path.Trim('"'));

        if (path == "~")
            return HomeDirectory;

        if (path.StartsWith("~/", StringComparison.Ordinal) ||
            path.StartsWith("~\\", StringComparison.Ordinal))
            path = Path.Combine(HomeDirectory, path[2..]);

        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(CurrentDirectory, path));
    }

    private string? ResolvePathSafe(string path)
    {
        try { return ResolvePath(path); }
        catch { return null; }
    }

    internal void SetCurrentDirectory(string path)
    {
        _previousDirectory = CurrentDirectory;
        CurrentDirectory = Path.GetFullPath(path);
    }

    internal string PreviousDirectory => _previousDirectory;

    internal Stack<string> DirectoryStack => _directoryStack;

    internal static List<string> Tokenize(string input)
    {
        List<string> tokens = new();
        StringBuilder current = new();
        bool inQuotes = false;

        foreach (char c in input)
        {
            if (c == '\"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }

    internal static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.##} {units[unit]}";
    }

    internal static string QuoteArgument(string value) =>
        value.Contains(' ') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;

    private ShellResult RunExternal(string[] tokens, bool explicitRequest)
    {
        if (tokens.Length == 0)
            return Error("exec: missing program name");

        string command = tokens[0];
        string? executable = FindExecutable(command);
        if (executable is null)
        {
            string prefix = explicitRequest ? "exec" : "kuro";
            return Error($"{prefix}: command not found: {command}\nType 'help' to list built-ins.");
        }

        bool commandScript = executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                             || executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
        ProcessStartInfo startInfo = new()
        {
            FileName = commandScript
                ? Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe"
                : executable,
            WorkingDirectory = CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };

        if (commandScript)
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(executable);
        }

        foreach (string argument in tokens.Skip(1))
            startInfo.ArgumentList.Add(argument);

        foreach ((string key, string value) in Config.Variables)
            startInfo.Environment[key] = value;

        using Process process = new() { StartInfo = startInfo };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(30_000))
        {
            process.Kill(true);
            return Error("Process exceeded the 30-second capture limit and was stopped.");
        }

        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();
        string combined = string.Join(Environment.NewLine,
            new[] { stdout.TrimEnd(), stderr.TrimEnd() }.Where(s => !string.IsNullOrEmpty(s)));
        return new ShellResult(combined, process.ExitCode != 0);
    }

    internal ShellResult RunExternalExplicit(string[] args) => RunExternal(args, true);

    internal string? FindExecutable(string name)
    {
        if (File.Exists(name))
            return Path.GetFullPath(name);

        string localCandidate = Path.Combine(CurrentDirectory, name);
        if (File.Exists(localCandidate))
            return Path.GetFullPath(localCandidate);

        string[] extensions = Path.HasExtension(name)
            ? [string.Empty]
            : (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
                .Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (string extension in extensions)
        {
            string candidate = Path.Combine(CurrentDirectory, name + extension);
            if (File.Exists(candidate))
                return candidate;
        }

        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string extension in extensions)
            {
                try
                {
                    string candidate = Path.Combine(directory.Trim('"'), name + extension);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                    // Ignore malformed PATH entries.
                }
            }
        }

        return null;
    }

    internal static ShellResult Ok(string output) => new(output);
    internal static ShellResult Error(string output) => new(output, IsError: true);
}
