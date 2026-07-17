using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace Kuro;

public sealed partial class ShellEngine
{
    private const long KuroTextEditLimit = 8L * 1024 * 1024;

    private void RegisterMegaProductivityCommands()
    {
        Register("edit", "Editor", "edit <file>", "Open a file in Kuro's built-in editor.", EditFileCommand, "kedit", "nano");
        Register("code", "Editor", "code [path]", "Open a file or folder in Visual Studio Code. Defaults to the current folder.", CodeCommand, "vscode");
        Register("openpath", "Desktop", "openpath [path|url]", "Open a file, folder, or URL with its Windows default application.", OpenPathCommand, "open");
        Register("explore", "Desktop", "explore [path]", "Open a folder in Windows File Explorer.", ExploreCommand, "explorer");
        Register("reveal", "Desktop", "reveal <file>", "Select a file in Windows File Explorer.", RevealCommand);
        Register("clipcopy", "Desktop", "clipcopy <text|--file path>", "Copy text or a file's contents to the Windows clipboard.", ClipboardCopyCommand);
        Register("clippaste", "Desktop", "clippaste", "Read text currently stored on the Windows clipboard.", ClipboardPasteCommand);
        Register("pathcopy", "Desktop", "pathcopy [path]", "Copy a resolved absolute path to the clipboard.", PathCopyCommand);

        Register("numberlines", "Text Files", "numberlines <file> [start] [count]", "Display a file with line numbers.", NumberLinesCommand, "nl");
        Register("insertline", "Text Files", "insertline <file> <line> <text>", "Insert a line into a text file and create a .kuro.bak backup.", InsertLineCommand);
        Register("deleteline", "Text Files", "deleteline <file> <line>", "Delete a line from a text file and create a .kuro.bak backup.", DeleteLineCommand);
        Register("replacetext", "Text Files", "replacetext <file> <old> <new>", "Replace literal text and create a .kuro.bak backup.", ReplaceTextCommand);
        Register("difftext", "Text Files", "difftext <first> <second>", "Show a simple line-by-line text comparison.", DiffTextCommand);

        Register("recentfiles", "Files+", "recentfiles [path] [count]", "List the most recently modified files below a folder.", RecentFilesCommand);
        Register("largestfiles", "Files+", "largestfiles [path] [count]", "List the largest files below a folder.", LargestFilesCommand);
        Register("emptydirs", "Files+", "emptydirs [path]", "Find empty folders recursively.", EmptyDirectoriesCommand);
        Register("touchmany", "Files+", "touchmany <file...>", "Create several empty files at once.", TouchManyCommand);
        Register("template", "Developer", "template <type> <file>", "Create a starter source or configuration file.", TemplateCommand);
    }

    private ShellResult EditFileCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("edit: use edit <file>");

        string path = ResolvePath(string.Join(' ', args));
        if (Directory.Exists(path))
            return Error("edit: the target is a folder");

        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                KuroEditorWindow editor = new(path)
                {
                    Owner = Application.Current.MainWindow
                };
                editor.ShowDialog();
            });

            return Ok($"editor closed: {path}");
        }
        catch (Exception ex)
        {
            return Error($"edit: {ex.Message}");
        }
    }

    private ShellResult CodeCommand(string[] args, string raw)
    {
        string target = args.Length == 0 ? CurrentDirectory : ResolvePath(string.Join(' ', args));
        string? code = FindExecutable("code") ?? MegaFindVisualStudioCode();

        if (code is null)
            return Error("code: Visual Studio Code was not found. Install VS Code and enable its 'code' command.");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = code,
                Arguments = QuoteArgument(target),
                WorkingDirectory = CurrentDirectory,
                UseShellExecute = true
            });

            return Ok($"opened in VS Code: {target}");
        }
        catch (Exception ex)
        {
            return Error($"code: {ex.Message}");
        }
    }

    private ShellResult OpenPathCommand(string[] args, string raw)
    {
        string target = args.Length == 0 ? CurrentDirectory : string.Join(' ', args);

        if (!Uri.TryCreate(target, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            target = ResolvePath(target);
            if (!File.Exists(target) && !Directory.Exists(target))
                return Error($"openpath: not found: {target}");
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
                WorkingDirectory = CurrentDirectory
            });
            return Ok($"opened: {target}");
        }
        catch (Exception ex)
        {
            return Error($"openpath: {ex.Message}");
        }
    }

    private ShellResult ExploreCommand(string[] args, string raw)
    {
        string target = args.Length == 0 ? CurrentDirectory : ResolvePath(string.Join(' ', args));
        if (File.Exists(target))
            target = Path.GetDirectoryName(target) ?? CurrentDirectory;

        if (!Directory.Exists(target))
            return Error($"explore: folder not found: {target}");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = QuoteArgument(target),
                UseShellExecute = true
            });
            return Ok($"opened Explorer: {target}");
        }
        catch (Exception ex)
        {
            return Error($"explore: {ex.Message}");
        }
    }

    private ShellResult RevealCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("reveal: use reveal <file>");

        string target = ResolvePath(string.Join(' ', args));
        if (!File.Exists(target) && !Directory.Exists(target))
            return Error($"reveal: not found: {target}");

        try
        {
            string arguments = File.Exists(target)
                ? "/select," + QuoteArgument(target)
                : QuoteArgument(target);

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = arguments,
                UseShellExecute = true
            });

            return Ok($"revealed: {target}");
        }
        catch (Exception ex)
        {
            return Error($"reveal: {ex.Message}");
        }
    }

    private ShellResult ClipboardCopyCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("clipcopy: use clipcopy <text> or clipcopy --file <path>");

        try
        {
            string text;
            if (args[0].Equals("--file", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                    return Error("clipcopy: missing file path");

                string path = ResolvePath(string.Join(' ', args.Skip(1)));
                if (!File.Exists(path))
                    return Error($"clipcopy: file not found: {path}");

                text = File.ReadAllText(path);
            }
            else
            {
                text = string.Join(' ', args);
            }

            Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(text));
            return Ok($"copied {text.Length:n0} characters to the clipboard");
        }
        catch (Exception ex)
        {
            return Error($"clipcopy: {ex.Message}");
        }
    }

    private ShellResult ClipboardPasteCommand(string[] args, string raw)
    {
        try
        {
            string text = string.Empty;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Clipboard.ContainsText())
                    text = Clipboard.GetText();
            });

            return string.IsNullOrEmpty(text)
                ? Error("clippaste: the clipboard does not contain text")
                : Ok(text);
        }
        catch (Exception ex)
        {
            return Error($"clippaste: {ex.Message}");
        }
    }

    private ShellResult PathCopyCommand(string[] args, string raw)
    {
        string path = args.Length == 0 ? CurrentDirectory : ResolvePath(string.Join(' ', args));
        try
        {
            Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(path));
            return Ok($"copied path: {path}");
        }
        catch (Exception ex)
        {
            return Error($"pathcopy: {ex.Message}");
        }
    }

    private ShellResult NumberLinesCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("numberlines: use numberlines <file> [start] [count]");

        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"numberlines: file not found: {path}");

        if (!MegaCanEditText(path, out string? sizeError))
            return Error("numberlines: " + sizeError);

        int start = args.Length > 1 && int.TryParse(args[1], out int parsedStart)
            ? Math.Max(1, parsedStart)
            : 1;
        int count = args.Length > 2 && int.TryParse(args[2], out int parsedCount)
            ? Math.Clamp(parsedCount, 1, 1000)
            : 200;

        string[] lines = File.ReadAllLines(path);
        IEnumerable<string> output = lines
            .Skip(start - 1)
            .Take(count)
            .Select((line, index) => $"{start + index,6}  {line}");

        string result = string.Join(Environment.NewLine, output);
        if (start - 1 + count < lines.Length)
            result += $"{Environment.NewLine}... output limited; file has {lines.Length:n0} lines";

        return Ok(result);
    }

    private ShellResult InsertLineCommand(string[] args, string raw)
    {
        if (args.Length < 3 || !int.TryParse(args[1], out int lineNumber))
            return Error("insertline: use insertline <file> <line> <text>");

        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"insertline: file not found: {path}");

        if (!MegaCanEditText(path, out string? sizeError))
            return Error("insertline: " + sizeError);

        List<string> lines = File.ReadAllLines(path).ToList();
        int index = Math.Clamp(lineNumber - 1, 0, lines.Count);
        MegaBackupTextFile(path);
        lines.Insert(index, string.Join(' ', args.Skip(2)));
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
        return Ok($"inserted line {index + 1}; backup: {path}.kuro.bak");
    }

    private ShellResult DeleteLineCommand(string[] args, string raw)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int lineNumber))
            return Error("deleteline: use deleteline <file> <line>");

        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"deleteline: file not found: {path}");

        if (!MegaCanEditText(path, out string? sizeError))
            return Error("deleteline: " + sizeError);

        List<string> lines = File.ReadAllLines(path).ToList();
        if (lineNumber < 1 || lineNumber > lines.Count)
            return Error($"deleteline: line must be between 1 and {lines.Count}");

        MegaBackupTextFile(path);
        lines.RemoveAt(lineNumber - 1);
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
        return Ok($"deleted line {lineNumber}; backup: {path}.kuro.bak");
    }

    private ShellResult ReplaceTextCommand(string[] args, string raw)
    {
        if (args.Length < 3)
            return Error("replacetext: use replacetext <file> <old> <new>");

        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"replacetext: file not found: {path}");

        if (!MegaCanEditText(path, out string? sizeError))
            return Error("replacetext: " + sizeError);

        string text = File.ReadAllText(path);
        int count = MegaCountOccurrences(text, args[1]);
        if (count == 0)
            return Error("replacetext: old text was not found");

        MegaBackupTextFile(path);
        File.WriteAllText(
            path,
            text.Replace(args[1], args[2], StringComparison.Ordinal),
            new UTF8Encoding(false));

        return Ok($"replaced {count:n0} occurrence(s); backup: {path}.kuro.bak");
    }

    private ShellResult DiffTextCommand(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("difftext: use difftext <first> <second>");

        string firstPath = ResolvePath(args[0]);
        string secondPath = ResolvePath(args[1]);

        if (!File.Exists(firstPath) || !File.Exists(secondPath))
            return Error("difftext: both files must exist");

        string[] first = File.ReadAllLines(firstPath);
        string[] second = File.ReadAllLines(secondPath);
        int total = Math.Max(first.Length, second.Length);
        StringBuilder output = new();
        int differences = 0;

        for (int index = 0; index < total && differences < 300; index++)
        {
            string left = index < first.Length ? first[index] : "<missing>";
            string right = index < second.Length ? second[index] : "<missing>";

            if (string.Equals(left, right, StringComparison.Ordinal))
                continue;

            differences++;
            output.AppendLine($"@@ line {index + 1} @@");
            output.AppendLine("- " + left);
            output.AppendLine("+ " + right);
        }

        if (differences == 0)
            return Ok("Files are identical.");

        if (differences >= 300)
            output.AppendLine("... diff limited to 300 changed lines");

        return Ok(output.ToString().TrimEnd());
    }

    private ShellResult RecentFilesCommand(string[] args, string raw)
    {
        string path = args.Length > 0 ? ResolvePath(args[0]) : CurrentDirectory;
        int count = args.Length > 1 && int.TryParse(args[1], out int parsed)
            ? Math.Clamp(parsed, 1, 200)
            : 25;

        if (!Directory.Exists(path))
            return Error($"recentfiles: folder not found: {path}");

        try
        {
            FileInfo[] files = new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(count)
                .ToArray();

            return files.Length == 0
                ? Ok("No files found.")
                : Ok(string.Join(
                    Environment.NewLine,
                    files.Select(file =>
                        $"{file.LastWriteTime:yyyy-MM-dd HH:mm}  {FormatSize(file.Length),10}  {file.FullName}")));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Error($"recentfiles: {ex.Message}");
        }
    }

    private ShellResult LargestFilesCommand(string[] args, string raw)
    {
        string path = args.Length > 0 ? ResolvePath(args[0]) : CurrentDirectory;
        int count = args.Length > 1 && int.TryParse(args[1], out int parsed)
            ? Math.Clamp(parsed, 1, 200)
            : 25;

        if (!Directory.Exists(path))
            return Error($"largestfiles: folder not found: {path}");

        try
        {
            FileInfo[] files = new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .OrderByDescending(file => file.Length)
                .Take(count)
                .ToArray();

            return files.Length == 0
                ? Ok("No files found.")
                : Ok(string.Join(
                    Environment.NewLine,
                    files.Select(file => $"{FormatSize(file.Length),10}  {file.FullName}")));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Error($"largestfiles: {ex.Message}");
        }
    }

    private ShellResult EmptyDirectoriesCommand(string[] args, string raw)
    {
        string path = args.Length > 0 ? ResolvePath(args[0]) : CurrentDirectory;
        if (!Directory.Exists(path))
            return Error($"emptydirs: folder not found: {path}");

        try
        {
            string[] empty = Directory
                .EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                .Where(directory => !Directory.EnumerateFileSystemEntries(directory).Any())
                .Take(500)
                .ToArray();

            return empty.Length == 0
                ? Ok("No empty folders found.")
                : Ok(string.Join(Environment.NewLine, empty));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Error($"emptydirs: {ex.Message}");
        }
    }

    private ShellResult TouchManyCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("touchmany: use touchmany <file...>");

        List<string> created = new();
        foreach (string item in args)
        {
            string path = ResolvePath(item);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using (File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            {
            }
            File.SetLastWriteTime(path, DateTime.Now);
            created.Add(path);
        }

        return Ok($"touched {created.Count} file(s):{Environment.NewLine}" +
                  string.Join(Environment.NewLine, created));
    }

    private ShellResult TemplateCommand(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("template: use template <csharp|python|html|json|markdown|gitignore> <file>");

        string type = args[0].ToLowerInvariant();
        string path = ResolvePath(string.Join(' ', args.Skip(1)));
        if (File.Exists(path))
            return Error($"template: file already exists: {path}");

        string content = type switch
        {
            "csharp" or "cs" =>
                "using System;\n\nnamespace App;\n\ninternal static class Program\n{\n    private static void Main()\n    {\n        Console.WriteLine(\"Hello from Kuro\");\n    }\n}\n",
            "python" or "py" =>
                "def main() -> None:\n    print(\"Hello from Kuro\")\n\n\nif __name__ == \"__main__\":\n    main()\n",
            "html" =>
                "<!doctype html>\n<html lang=\"en\">\n<head>\n  <meta charset=\"utf-8\">\n  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n  <title>Kuro Project</title>\n</head>\n<body>\n  <h1>Hello from Kuro</h1>\n</body>\n</html>\n",
            "json" =>
                "{\n  \"name\": \"kuro-project\",\n  \"version\": \"1.0.0\"\n}\n",
            "markdown" or "md" =>
                "# Kuro Project\n\nCreated from the Kuro terminal.\n",
            "gitignore" =>
                "bin/\nobj/\n.vs/\nartifacts/\nReleases/\nDIST-TO-SHARE/\n*.user\n*.log\n",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(content))
            return Error("template: unknown template type");

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, content, new UTF8Encoding(false));
        return Ok($"created {type} template: {path}");
    }

    private static bool MegaCanEditText(string path, out string? error)
    {
        long length = new FileInfo(path).Length;
        if (length > KuroTextEditLimit)
        {
            error = $"file is larger than {FormatSize(KuroTextEditLimit)}";
            return false;
        }

        error = null;
        return true;
    }

    private static void MegaBackupTextFile(string path) =>
        File.Copy(path, path + ".kuro.bak", true);

    private static int MegaCountOccurrences(string text, string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string? MegaFindVisualStudioCode()
    {
        string[] candidates =
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code", "Code.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft VS Code", "Code.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
