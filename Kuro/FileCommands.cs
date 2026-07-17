using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Kuro;

public sealed partial class ShellEngine
{
    private void RegisterFileCommands()
    {
        Register("pwd", "Files", "pwd", "Show the current directory.", (_, _) => Ok(CurrentDirectory));
        Register("cd", "Files", "cd [path|-]", "Change the current directory.", ChangeDirectory);
        Register("pushd", "Files", "pushd <path>", "Push the current path and change directory.", PushDirectory);
        Register("popd", "Files", "popd", "Return to the last pushed directory.", PopDirectory);
        Register("dirs", "Files", "dirs", "Show the directory stack.", ShowDirectories);
        Register("ls", "Files", "ls [-a] [-l] [path]", "List files and folders.", ListDirectory, "dir");
        Register("tree", "Files", "tree [path] [-d depth]", "Draw a directory tree.", Tree);
        Register("mkdir", "Files", "mkdir <folder...>", "Create one or more folders.", MakeDirectory, "md");
        Register("rmdir", "Files", "rmdir [-r] <folder...>", "Remove empty or recursive folders.", RemoveDirectory, "rd");
        Register("touch", "Files", "touch <file...>", "Create files or update timestamps.", Touch);
        Register("cat", "Files", "cat <file...>", "Read one or more text files.", Cat, "type");
        Register("write", "Files", "write <file> <text>", "Replace a file with text.", WriteFile);
        Register("append", "Files", "append <file> <text>", "Append a line of text to a file.", AppendFile);
        Register("cp", "Files", "cp [-r] <source> <destination>", "Copy files or folders.", Copy, "copy");
        Register("mv", "Files", "mv <source> <destination>", "Move a file or folder.", Move, "move");
        Register("rename", "Files", "rename <path> <new-name>", "Rename a file or folder.", Rename, "ren");
        Register("rm", "Files", "rm [-r] [-f] <target...>", "Delete files or folders with safeguards.", Remove, "del");
        Register("stat", "Files", "stat <path>", "Show file or folder details.", Stat);
        Register("exists", "Files", "exists <path>", "Check whether a path exists.", Exists);
        Register("find", "Search/Text", "find <pattern> [path]", "Recursively find files and folders.", Find);
        Register("grep", "Search/Text", "grep [-i] <text> <file>", "Search for text inside a file.", Grep);
        Register("head", "Search/Text", "head [-n count] <file>", "Show the first lines of a file.", Head);
        Register("tail", "Search/Text", "tail [-n count] <file>", "Show the last lines of a file.", Tail);
        Register("wc", "Search/Text", "wc <file>", "Count lines, words, characters, and bytes.", WordCount);
        Register("sort", "Search/Text", "sort [-r] <file>", "Sort the lines of a file.", SortFile);
        Register("uniq", "Search/Text", "uniq <file>", "Remove consecutive duplicate lines.", UniqueLines);
        Register("basename", "Files", "basename <path>", "Print the final path component.", BaseName);
        Register("dirname", "Files", "dirname <path>", "Print the parent directory.", DirectoryName);
        Register("drives", "Files", "drives", "List available Windows drives.", Drives);
    }

    private ShellResult ChangeDirectory(string[] args, string raw)
    {
        string destination;
        if (args.Length == 0)
            destination = HomeDirectory;
        else if (args[0] == "-")
            destination = PreviousDirectory;
        else
            destination = ResolvePath(args[0]);

        if (!Directory.Exists(destination))
            return Error($"cd: no such directory: {(args.Length == 0 ? destination : args[0])}");

        SetCurrentDirectory(destination);
        return args.Length > 0 && args[0] == "-" ? Ok(CurrentDirectory) : ShellResult.Empty;
    }

    private ShellResult PushDirectory(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("pushd: missing directory");

        string destination = ResolvePath(args[0]);
        if (!Directory.Exists(destination))
            return Error($"pushd: no such directory: {args[0]}");

        DirectoryStack.Push(CurrentDirectory);
        SetCurrentDirectory(destination);
        return ShowDirectories(Array.Empty<string>(), string.Empty);
    }

    private ShellResult PopDirectory(string[] args, string raw)
    {
        if (DirectoryStack.Count == 0)
            return Error("popd: directory stack is empty");

        string destination = DirectoryStack.Pop();
        SetCurrentDirectory(destination);
        return ShowDirectories(Array.Empty<string>(), string.Empty);
    }

    private ShellResult ShowDirectories(string[] args, string raw)
    {
        IEnumerable<string> paths = new[] { CurrentDirectory }.Concat(DirectoryStack);
        return Ok(string.Join("  ", paths.Select(DisplayFriendlyPath)));
    }

    private string DisplayFriendlyPath(string path)
    {
        if (string.Equals(path, HomeDirectory, StringComparison.OrdinalIgnoreCase))
            return "~";
        if (path.StartsWith(HomeDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return "~/" + path[(HomeDirectory.Length + 1)..].Replace(Path.DirectorySeparatorChar, '/');
        return path;
    }

    private ShellResult ListDirectory(string[] args, string raw)
    {
        bool showHidden = args.Any(a => a.Equals("-a", StringComparison.OrdinalIgnoreCase) || a.Equals("-la", StringComparison.OrdinalIgnoreCase) || a.Equals("-al", StringComparison.OrdinalIgnoreCase));
        bool longFormat = args.Any(a => a.Equals("-l", StringComparison.OrdinalIgnoreCase) || a.Equals("-la", StringComparison.OrdinalIgnoreCase) || a.Equals("-al", StringComparison.OrdinalIgnoreCase));
        string? pathArgument = args.LastOrDefault(a => !a.StartsWith('-'));
        string path = pathArgument is null ? CurrentDirectory : ResolvePath(pathArgument);

        if (File.Exists(path))
            return Ok(FormatEntry(new FileInfo(path), longFormat));
        if (!Directory.Exists(path))
            return Error($"ls: no such file or directory: {pathArgument ?? path}");

        DirectoryInfo directory = new(path);
        List<FileSystemInfo> entries = directory.EnumerateFileSystemInfos()
            .Where(entry => showHidden || !entry.Attributes.HasFlag(FileAttributes.Hidden))
            .OrderByDescending(entry => entry is DirectoryInfo)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (entries.Count == 0)
            return Ok("(empty directory)");

        return Ok(string.Join(Environment.NewLine, entries.Select(entry => FormatEntry(entry, longFormat))));
    }

    private static string FormatEntry(FileSystemInfo entry, bool longFormat)
    {
        bool isDirectory = entry is DirectoryInfo;
        if (!longFormat)
            return isDirectory ? $"[DIR]  {entry.Name}" : $"       {entry.Name}";

        string attributes = $"{(isDirectory ? 'd' : '-')}{(entry.Attributes.HasFlag(FileAttributes.ReadOnly) ? 'r' : 'w')}{(entry.Attributes.HasFlag(FileAttributes.Hidden) ? 'h' : '-')}{(entry.Attributes.HasFlag(FileAttributes.System) ? 's' : '-')}";
        string size = entry is FileInfo file ? FormatSize(file.Length) : "<DIR>";
        return $"{attributes}  {entry.LastWriteTime:yyyy-MM-dd HH:mm}  {size,10}  {entry.Name}";
    }

    private ShellResult MakeDirectory(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("mkdir: missing directory name");

        foreach (string arg in args)
            Directory.CreateDirectory(ResolvePath(arg));
        return ShellResult.Empty;
    }

    private ShellResult RemoveDirectory(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("rmdir: missing directory name");

        bool recursive = args.Any(a => a.Equals("-r", StringComparison.OrdinalIgnoreCase) || a.Equals("-rf", StringComparison.OrdinalIgnoreCase));
        string[] targets = args.Where(a => !a.StartsWith('-')).ToArray();
        foreach (string target in targets)
        {
            string path = ResolvePath(target);
            if (!Directory.Exists(path))
                return Error($"rmdir: no such directory: {target}");
            if (IsProtectedDeleteTarget(path))
                return Error($"rmdir: refusing to delete protected location: {path}");
            Directory.Delete(path, recursive);
        }
        return ShellResult.Empty;
    }

    private ShellResult Touch(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("touch: missing file name");

        foreach (string arg in args)
        {
            string path = ResolvePath(arg);
            string? parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            if (File.Exists(path))
                File.SetLastWriteTime(path, DateTime.Now);
            else
                File.WriteAllText(path, string.Empty);
        }
        return ShellResult.Empty;
    }

    private ShellResult Cat(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("cat: missing file name");

        StringBuilder output = new();
        foreach (string arg in args)
        {
            string path = ResolvePath(arg);
            if (!File.Exists(path))
                return Error($"cat: no such file: {arg}");

            if (args.Length > 1)
                output.AppendLine($"==> {arg} <==");
            output.Append(File.ReadAllText(path));
            if (!output.ToString().EndsWith(Environment.NewLine, StringComparison.Ordinal))
                output.AppendLine();
        }
        return Ok(output.ToString().TrimEnd());
    }

    private ShellResult WriteFile(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("write: use write <file> <text>");

        string path = ResolvePath(args[0]);
        EnsureParentDirectory(path);
        File.WriteAllText(path, string.Join(' ', args.Skip(1)));
        return Ok($"Wrote {FormatSize(new FileInfo(path).Length)} to {path}");
    }

    private ShellResult AppendFile(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("append: use append <file> <text>");

        string path = ResolvePath(args[0]);
        EnsureParentDirectory(path);
        File.AppendAllText(path, string.Join(' ', args.Skip(1)) + Environment.NewLine);
        return Ok($"Appended to {path}");
    }

    private ShellResult Copy(string[] args, string raw)
    {
        bool recursive = args.Any(a => a.Equals("-r", StringComparison.OrdinalIgnoreCase));
        string[] operands = args.Where(a => !a.StartsWith('-')).ToArray();
        if (operands.Length != 2)
            return Error("cp: use cp [-r] <source> <destination>");

        string source = ResolvePath(operands[0]);
        string destination = ResolvePath(operands[1]);

        if (File.Exists(source))
        {
            if (Directory.Exists(destination))
                destination = Path.Combine(destination, Path.GetFileName(source));
            EnsureParentDirectory(destination);
            File.Copy(source, destination, true);
            return ShellResult.Empty;
        }

        if (!Directory.Exists(source))
            return Error($"cp: source not found: {operands[0]}");
        if (!recursive)
            return Error("cp: source is a directory; use cp -r");

        CopyDirectory(source, destination);
        return ShellResult.Empty;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (string directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private ShellResult Move(string[] args, string raw)
    {
        if (args.Length != 2)
            return Error("mv: use mv <source> <destination>");

        string source = ResolvePath(args[0]);
        string destination = ResolvePath(args[1]);
        if (Directory.Exists(destination))
            destination = Path.Combine(destination, Path.GetFileName(source));
        EnsureParentDirectory(destination);

        if (File.Exists(source))
            File.Move(source, destination, true);
        else if (Directory.Exists(source))
            Directory.Move(source, destination);
        else
            return Error($"mv: source not found: {args[0]}");

        return ShellResult.Empty;
    }

    private ShellResult Rename(string[] args, string raw)
    {
        if (args.Length != 2)
            return Error("rename: use rename <path> <new-name>");

        string source = ResolvePath(args[0]);
        string parent = Path.GetDirectoryName(source) ?? CurrentDirectory;
        string destination = Path.Combine(parent, args[1]);

        if (File.Exists(source))
            File.Move(source, destination);
        else if (Directory.Exists(source))
            Directory.Move(source, destination);
        else
            return Error($"rename: path not found: {args[0]}");

        return ShellResult.Empty;
    }

    private ShellResult Remove(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("rm: missing target");

        bool recursive = args.Any(a => a.Equals("-r", StringComparison.OrdinalIgnoreCase) || a.Equals("-rf", StringComparison.OrdinalIgnoreCase));
        bool force = args.Any(a => a.Equals("-f", StringComparison.OrdinalIgnoreCase) || a.Equals("-rf", StringComparison.OrdinalIgnoreCase));
        string[] targets = args.Where(a => !a.StartsWith('-')).ToArray();
        if (targets.Length == 0)
            return Error("rm: missing target");

        foreach (string target in targets)
        {
            string path = ResolvePath(target);
            if (IsProtectedDeleteTarget(path))
                return Error($"rm: refusing to delete protected location: {path}");

            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                if (!recursive)
                    return Error($"rm: '{target}' is a directory; use rm -r");
                Directory.Delete(path, true);
            }
            else if (!force)
            {
                return Error($"rm: no such file or directory: {target}");
            }
        }
        return ShellResult.Empty;
    }

    private bool IsProtectedDeleteTarget(string path)
    {
        string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        string root = (Path.GetPathRoot(full) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar);
        string home = HomeDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string current = CurrentDirectory.TrimEnd(Path.DirectorySeparatorChar);
        return string.Equals(full, root, StringComparison.OrdinalIgnoreCase)
               || string.Equals(full, home, StringComparison.OrdinalIgnoreCase)
               || string.Equals(full, current, StringComparison.OrdinalIgnoreCase);
    }

    private ShellResult Stat(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("stat: missing path");

        string path = ResolvePath(args[0]);
        if (File.Exists(path))
        {
            FileInfo file = new(path);
            return Ok($"Path:       {file.FullName}\nType:       file\nSize:       {file.Length} bytes ({FormatSize(file.Length)})\nCreated:    {file.CreationTime}\nModified:   {file.LastWriteTime}\nAccessed:   {file.LastAccessTime}\nAttributes: {file.Attributes}");
        }

        if (Directory.Exists(path))
        {
            DirectoryInfo directory = new(path);
            return Ok($"Path:       {directory.FullName}\nType:       directory\nCreated:    {directory.CreationTime}\nModified:   {directory.LastWriteTime}\nAttributes: {directory.Attributes}\nEntries:    {directory.EnumerateFileSystemInfos().Count()}");
        }

        return Error($"stat: path not found: {args[0]}");
    }

    private ShellResult Exists(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("exists: missing path");
        string path = ResolvePath(args[0]);
        if (File.Exists(path))
            return Ok("true (file)");
        if (Directory.Exists(path))
            return Ok("true (directory)");
        return new ShellResult("false", IsError: true);
    }

    private ShellResult Tree(string[] args, string raw)
    {
        string? pathArgument = args.FirstOrDefault(a => !a.StartsWith('-') && !int.TryParse(a, out _));
        int maxDepth = 4;
        int depthFlag = Array.FindIndex(args, a => a.Equals("-d", StringComparison.OrdinalIgnoreCase));
        if (depthFlag >= 0 && depthFlag + 1 < args.Length && int.TryParse(args[depthFlag + 1], out int parsedDepth))
            maxDepth = Math.Clamp(parsedDepth, 1, 20);

        string root = pathArgument is null ? CurrentDirectory : ResolvePath(pathArgument);
        if (!Directory.Exists(root))
            return Error($"tree: no such directory: {pathArgument ?? root}");

        StringBuilder output = new();
        output.AppendLine(new DirectoryInfo(root).Name);
        BuildTree(root, output, string.Empty, 0, maxDepth);
        return Ok(output.ToString().TrimEnd());
    }

    private static void BuildTree(string path, StringBuilder output, string indent, int depth, int maxDepth)
    {
        if (depth >= maxDepth)
            return;

        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(path)
                .OrderByDescending(Directory.Exists)
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            output.AppendLine($"{indent}└── [access denied]");
            return;
        }

        for (int index = 0; index < entries.Length; index++)
        {
            string entry = entries[index];
            bool last = index == entries.Length - 1;
            output.AppendLine($"{indent}{(last ? "└── " : "├── ")}{Path.GetFileName(entry)}");
            if (Directory.Exists(entry))
                BuildTree(entry, output, indent + (last ? "    " : "│   "), depth + 1, maxDepth);
        }
    }

    private ShellResult Find(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("find: use find <pattern> [path]");

        string pattern = args[0];
        string root = args.Length > 1 ? ResolvePath(args[1]) : CurrentDirectory;
        if (!Directory.Exists(root))
            return Error($"find: no such directory: {root}");

        List<string> results = new();
        FindRecursive(root, pattern, results, 500);
        return results.Count == 0
            ? Ok("No matches.")
            : Ok(string.Join(Environment.NewLine, results));
    }

    private static void FindRecursive(string path, string pattern, List<string> results, int limit)
    {
        if (results.Count >= limit)
            return;

        try
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(path))
            {
                if (Path.GetFileName(entry).Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    results.Add(entry);
                if (results.Count >= limit)
                    return;
                if (Directory.Exists(entry))
                    FindRecursive(entry, pattern, results, limit);
            }
        }
        catch
        {
            // Skip inaccessible folders.
        }
    }

    private ShellResult Grep(string[] args, string raw)
    {
        bool ignoreCase = args.Any(a => a.Equals("-i", StringComparison.OrdinalIgnoreCase));
        string[] operands = args.Where(a => !a.StartsWith('-')).ToArray();
        if (operands.Length < 2)
            return Error("grep: use grep [-i] <text> <file>");

        string needle = operands[0];
        string path = ResolvePath(operands[1]);
        if (!File.Exists(path))
            return Error($"grep: file not found: {operands[1]}");

        StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string[] matches = File.ReadLines(path)
            .Select((line, index) => (line, number: index + 1))
            .Where(item => item.line.Contains(needle, comparison))
            .Select(item => $"{item.number}: {item.line}")
            .ToArray();

        return matches.Length == 0 ? Ok("No matches.") : Ok(string.Join(Environment.NewLine, matches));
    }

    private ShellResult Head(string[] args, string raw) => ReadEdge(args, true);

    private ShellResult Tail(string[] args, string raw) => ReadEdge(args, false);

    private ShellResult ReadEdge(string[] args, bool head)
    {
        int count = 10;
        int countFlag = Array.FindIndex(args, a => a.Equals("-n", StringComparison.OrdinalIgnoreCase));
        if (countFlag >= 0 && countFlag + 1 < args.Length && int.TryParse(args[countFlag + 1], out int parsed))
            count = Math.Clamp(parsed, 1, 10_000);

        string? fileArg = args.LastOrDefault(a => !a.StartsWith('-') && !int.TryParse(a, out _));
        if (fileArg is null)
            return Error($"{(head ? "head" : "tail")}: missing file");

        string path = ResolvePath(fileArg);
        if (!File.Exists(path))
            return Error($"{(head ? "head" : "tail")}: file not found: {fileArg}");

        string[] lines = File.ReadAllLines(path);
        IEnumerable<string> selected = head ? lines.Take(count) : lines.TakeLast(count);
        return Ok(string.Join(Environment.NewLine, selected));
    }

    private ShellResult WordCount(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("wc: missing file");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"wc: file not found: {args[0]}");

        string text = File.ReadAllText(path);
        int lines = text.Length == 0 ? 0 : text.Split('\n').Length;
        int words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return Ok($"lines       {lines}\nwords       {words}\ncharacters  {text.Length}\nbytes       {new FileInfo(path).Length}");
    }

    private ShellResult SortFile(string[] args, string raw)
    {
        bool reverse = args.Any(a => a.Equals("-r", StringComparison.OrdinalIgnoreCase));
        string? fileArg = args.LastOrDefault(a => !a.StartsWith('-'));
        if (fileArg is null)
            return Error("sort: missing file");
        string path = ResolvePath(fileArg);
        if (!File.Exists(path))
            return Error($"sort: file not found: {fileArg}");

        IEnumerable<string> lines = File.ReadLines(path).OrderBy(line => line, StringComparer.OrdinalIgnoreCase);
        if (reverse)
            lines = lines.Reverse();
        return Ok(string.Join(Environment.NewLine, lines));
    }

    private ShellResult UniqueLines(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("uniq: missing file");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"uniq: file not found: {args[0]}");

        List<string> output = new();
        string? previous = null;
        bool first = true;
        foreach (string line in File.ReadLines(path))
        {
            if (first || !string.Equals(line, previous, StringComparison.Ordinal))
                output.Add(line);
            previous = line;
            first = false;
        }
        return Ok(string.Join(Environment.NewLine, output));
    }

    private ShellResult BaseName(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("basename: missing path");
        string trimmed = args[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Ok(Path.GetFileName(trimmed));
    }

    private ShellResult DirectoryName(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("dirname: missing path");
        return Ok(Path.GetDirectoryName(ResolvePath(args[0])) ?? string.Empty);
    }

    private ShellResult Drives(string[] args, string raw)
    {
        IEnumerable<string> lines = DriveInfo.GetDrives().Select(drive =>
        {
            try
            {
                return $"{drive.Name,-5} {drive.DriveType,-10} {drive.VolumeLabel,-18} {FormatSize(drive.AvailableFreeSpace),10} free / {FormatSize(drive.TotalSize)}";
            }
            catch
            {
                return $"{drive.Name,-5} {drive.DriveType,-10} unavailable";
            }
        });
        return Ok(string.Join(Environment.NewLine, lines));
    }

    private static void EnsureParentDirectory(string path)
    {
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);
    }
}
