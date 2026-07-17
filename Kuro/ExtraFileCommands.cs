using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Kuro;

public sealed partial class ShellEngine
{
    private void RegisterExtraFileCommands()
    {
        Register("mktemp", "Files+", "mktemp [file|dir]", "Create a uniquely named temporary item in the current folder.", MakeTemporary);
        Register("temp", "Files+", "temp", "Show the Windows temporary directory.", (_, _) => Ok(Path.GetTempPath()), "tempdir");
        Register("du", "Files+", "du [path]", "Calculate recursive disk usage for a path.", DiskUsage, "foldersize");
        Register("diskfree", "Files+", "diskfree [path]", "Show free and total space for a drive.", DiskFree, "df");
        Register("filesize", "Files+", "filesize <file>", "Show the exact and formatted size of a file.", FileSizeCommand);
        Register("checksum", "Files+", "checksum <file>", "Calculate SHA-256, SHA-1, and MD5 file checksums.", ChecksumFile);
        Register("compare", "Files+", "compare <file1> <file2>", "Compare two files by size and SHA-256.", CompareFiles, "cmp");
        Register("filetype", "Files+", "filetype <file>", "Guess a file type using extension and magic bytes.", FileTypeCommand, "file");
        Register("hexdump", "Files+", "hexdump <file> [bytes]", "Display a bounded hexadecimal file preview.", HexDump, "xxd");
        Register("strings", "Files+", "strings <file> [min-length]", "Extract printable ASCII strings from a local file.", ExtractStrings);
        Register("replace", "Search/Text", "replace <file> <old> <new>", "Replace text inside a UTF-8 text file.", ReplaceInFile);
        Register("line", "Search/Text", "line <number> <file>", "Print one numbered line from a text file.", ReadLineCommand);
        Register("join", "Files+", "join <destination> <file...>", "Join multiple files into one binary file.", JoinFiles);
        Register("splitfile", "Files+", "splitfile <file> <chunk-bytes>", "Split a file into numbered chunks.", SplitFile);
        Register("zip", "Archives", "zip <archive.zip> <path...>", "Create a ZIP archive from files and folders.", CreateZip);
        Register("unzip", "Archives", "unzip <archive.zip> [destination]", "Safely extract a ZIP archive.", ExtractZip);
        Register("ziplist", "Archives", "ziplist <archive.zip>", "List files inside a ZIP archive.", ListZip);
        Register("backup", "Archives", "backup <path>", "Create a timestamped file copy or folder ZIP backup.", BackupPath);
        Register("pathinfo", "Files+", "pathinfo <path>", "Show normalized path components and status.", PathInfoCommand);
        Register("realpath", "Files+", "realpath <path>", "Print a normalized absolute path.", RealPathCommand, "abspath");
        Register("extension", "Files+", "extension <path>", "Print a path's extension.", ExtensionCommand, "ext");
        Register("countfiles", "Files+", "countfiles [path]", "Count files and folders recursively.", CountFilesCommand);
        Register("newest", "Files+", "newest [path]", "Find the newest item under a folder.", NewestItem);
        Register("oldest", "Files+", "oldest [path]", "Find the oldest item under a folder.", OldestItem);
        Register("emptyfiles", "Files+", "emptyfiles [path]", "List empty files under a folder.", EmptyFiles);
        Register("duplicates", "Files+", "duplicates [path]", "Find duplicate files by size and SHA-256 with safe limits.", DuplicateFiles);
    }

    private ShellResult MakeTemporary(string[] args, string raw)
    {
        bool directory = args.Length > 0 && args[0].Equals("dir", StringComparison.OrdinalIgnoreCase);
        string name = ".kuro-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" +
                      Guid.NewGuid().ToString("N")[..6];
        string path = Path.Combine(CurrentDirectory, name);
        if (directory)
            Directory.CreateDirectory(path);
        else
            File.WriteAllText(path + ".tmp", string.Empty);

        return Ok(directory ? path : path + ".tmp");
    }

    private ShellResult DiskUsage(string[] args, string raw)
    {
        string path = args.Length == 0 ? CurrentDirectory : ResolvePath(args[0]);
        if (File.Exists(path))
            return Ok($"{new FileInfo(path).Length} bytes ({FormatSize(new FileInfo(path).Length)})");
        if (!Directory.Exists(path))
            return Error($"du: path not found: {path}");

        long bytes = 0;
        int files = 0;
        List<string> measuredFiles = CollectFilesSafe(path, 100_000, out int skipped);
        foreach (string file in measuredFiles)
        {
            try
            {
                bytes += new FileInfo(file).Length;
                files++;
            }
            catch
            {
                skipped++;
            }
        }

        return Ok($"Path:    {path}\nFiles:   {files:n0}\nSize:    {bytes:n0} bytes ({FormatSize(bytes)})\nSkipped: {skipped:n0}");
    }

    private ShellResult DiskFree(string[] args, string raw)
    {
        string path = args.Length == 0 ? CurrentDirectory : ResolvePath(args[0]);
        string root = Path.GetPathRoot(path) ?? path;
        DriveInfo drive = new(root);
        if (!drive.IsReady)
            return Error($"diskfree: drive is not ready: {root}");

        double usedPercent = drive.TotalSize == 0
            ? 0
            : (drive.TotalSize - drive.AvailableFreeSpace) * 100d / drive.TotalSize;
        return Ok($"Drive:     {drive.Name}\nFormat:    {drive.DriveFormat}\nLabel:     {drive.VolumeLabel}\nTotal:     {FormatSize(drive.TotalSize)}\nFree:      {FormatSize(drive.AvailableFreeSpace)}\nUsed:      {usedPercent:0.0}%");
    }

    private ShellResult FileSizeCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("filesize: missing file path");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"filesize: file not found: {args[0]}");
        long length = new FileInfo(path).Length;
        return Ok($"{length:n0} bytes\n{FormatSize(length)}");
    }

    private ShellResult ChecksumFile(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("checksum: missing file path");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"checksum: file not found: {args[0]}");

        return Ok($"SHA256  {ComputeFileHash(path, SHA256.Create())}\n" +
                  $"SHA1    {ComputeFileHash(path, SHA1.Create())}\n" +
                  $"MD5     {ComputeFileHash(path, MD5.Create())}");
    }

    private ShellResult CompareFiles(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("compare: use compare <file1> <file2>");
        string first = ResolvePath(args[0]);
        string second = ResolvePath(args[1]);
        if (!File.Exists(first) || !File.Exists(second))
            return Error("compare: both files must exist");

        FileInfo one = new(first);
        FileInfo two = new(second);
        string hashOne = ComputeFileHash(first, SHA256.Create());
        string hashTwo = ComputeFileHash(second, SHA256.Create());
        bool equal = one.Length == two.Length && hashOne.Equals(hashTwo, StringComparison.OrdinalIgnoreCase);
        return new ShellResult(
            $"File 1: {first}\nSize 1: {one.Length:n0}\nSHA256: {hashOne}\n\n" +
            $"File 2: {second}\nSize 2: {two.Length:n0}\nSHA256: {hashTwo}\n\n" +
            $"Identical: {equal}",
            !equal);
    }

    private ShellResult FileTypeCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("filetype: missing file path");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"filetype: file not found: {args[0]}");

        byte[] header = new byte[16];
        int read;
        using (FileStream stream = File.OpenRead(path))
            read = stream.Read(header, 0, header.Length);

        string detected = DetectMagic(header.AsSpan(0, read));
        string extension = Path.GetExtension(path);
        return Ok($"Name:      {Path.GetFileName(path)}\nExtension: {(string.IsNullOrEmpty(extension) ? "(none)" : extension)}\nDetected:  {detected}\nSize:      {FormatSize(new FileInfo(path).Length)}");
    }

    private ShellResult HexDump(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("hexdump: missing file path");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"hexdump: file not found: {args[0]}");

        int count = 512;
        if (args.Length > 1 && int.TryParse(args[1], out int parsed))
            count = Math.Clamp(parsed, 1, 4096);
        byte[] data = File.ReadAllBytes(path).Take(count).ToArray();
        StringBuilder output = new();
        for (int offset = 0; offset < data.Length; offset += 16)
        {
            ReadOnlySpan<byte> row = data.AsSpan(offset, Math.Min(16, data.Length - offset));
            string hex = string.Join(" ", row.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture))).PadRight(47);
            string text = new(row.ToArray().Select(value => value is >= 32 and <= 126 ? (char)value : '.').ToArray());
            output.AppendLine($"{offset:X8}  {hex}  |{text}|");
        }
        if (new FileInfo(path).Length > count)
            output.AppendLine($"... preview limited to {count} bytes");
        return Ok(output.ToString().TrimEnd());
    }

    private ShellResult ExtractStrings(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("strings: missing file path");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"strings: file not found: {args[0]}");
        int minimum = 4;
        if (args.Length > 1 && int.TryParse(args[1], out int parsed))
            minimum = Math.Clamp(parsed, 3, 64);

        long maxBytes = 16 * 1024 * 1024;
        byte[] data;
        using (FileStream stream = File.OpenRead(path))
        {
            int length = (int)Math.Min(stream.Length, maxBytes);
            data = new byte[length];
            _ = stream.Read(data, 0, length);
        }

        List<string> values = new();
        StringBuilder current = new();
        foreach (byte value in data)
        {
            if (value is >= 32 and <= 126)
            {
                current.Append((char)value);
            }
            else
            {
                if (current.Length >= minimum)
                    values.Add(current.ToString());
                current.Clear();
                if (values.Count >= 500)
                    break;
            }
        }
        if (current.Length >= minimum && values.Count < 500)
            values.Add(current.ToString());

        return Ok(values.Count == 0
            ? "No printable strings found."
            : string.Join(Environment.NewLine, values) + (values.Count >= 500 ? "\n... output limited to 500 strings" : string.Empty));
    }

    private ShellResult ReplaceInFile(string[] args, string raw)
    {
        if (args.Length < 3)
            return Error("replace: use replace <file> <old> <new>");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"replace: file not found: {args[0]}");
        string text = File.ReadAllText(path);
        int occurrences = CountOccurrences(text, args[1]);
        if (occurrences == 0)
            return Ok("No matches found; file unchanged.");
        File.WriteAllText(path, text.Replace(args[1], args[2], StringComparison.Ordinal));
        return Ok($"Replaced {occurrences} occurrence(s).");
    }

    private ShellResult ReadLineCommand(string[] args, string raw)
    {
        if (args.Length < 2 || !int.TryParse(args[0], out int lineNumber) || lineNumber < 1)
            return Error("line: use line <positive-number> <file>");
        string path = ResolvePath(args[1]);
        if (!File.Exists(path))
            return Error($"line: file not found: {args[1]}");
        string? line = File.ReadLines(path).Skip(lineNumber - 1).FirstOrDefault();
        return line is null ? Error($"line: file has fewer than {lineNumber} lines") : Ok(line);
    }

    private ShellResult JoinFiles(string[] args, string raw)
    {
        if (args.Length < 3)
            return Error("join: use join <destination> <file1> <file2> [...]");
        string destination = ResolvePath(args[0]);
        using FileStream output = File.Create(destination);
        foreach (string argument in args.Skip(1))
        {
            string source = ResolvePath(argument);
            if (!File.Exists(source))
                return Error($"join: file not found: {argument}");
            using FileStream input = File.OpenRead(source);
            input.CopyTo(output);
        }
        return Ok($"Created {destination} ({FormatSize(new FileInfo(destination).Length)}).");
    }

    private ShellResult SplitFile(string[] args, string raw)
    {
        if (args.Length < 2 || !long.TryParse(args[1], out long chunkSize) || chunkSize < 1)
            return Error("splitfile: use splitfile <file> <chunk-bytes>");
        chunkSize = Math.Clamp(chunkSize, 1, 512L * 1024 * 1024);
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"splitfile: file not found: {args[0]}");

        byte[] buffer = new byte[Math.Min((int)Math.Min(chunkSize, 1024 * 1024), 1024 * 1024)];
        int part = 0;
        using FileStream input = File.OpenRead(path);
        while (input.Position < input.Length)
        {
            part++;
            string outputPath = path + $".part{part:000}";
            using FileStream output = File.Create(outputPath);
            long remaining = Math.Min(chunkSize, input.Length - input.Position);
            while (remaining > 0)
            {
                int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read == 0)
                    break;
                output.Write(buffer, 0, read);
                remaining -= read;
            }
        }
        return Ok($"Created {part} chunk(s) beside the source file.");
    }

    private ShellResult CreateZip(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("zip: use zip <archive.zip> <path...>");
        string archivePath = ResolvePath(args[0]);
        if (!archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            archivePath += ".zip";
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath) ?? CurrentDirectory);

        using ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (string item in args.Skip(1))
        {
            string path = ResolvePath(item);
            if (File.Exists(path))
            {
                archive.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Optimal);
            }
            else if (Directory.Exists(path))
            {
                string baseName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(path, file);
                    archive.CreateEntryFromFile(file, Path.Combine(baseName, relative), CompressionLevel.Optimal);
                }
            }
            else
            {
                return Error($"zip: path not found: {item}");
            }
        }
        return Ok($"Created {archivePath} ({FormatSize(new FileInfo(archivePath).Length)}).");
    }

    private ShellResult ExtractZip(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("unzip: missing archive path");
        string archivePath = ResolvePath(args[0]);
        if (!File.Exists(archivePath))
            return Error($"unzip: archive not found: {args[0]}");
        string destination = args.Length > 1
            ? ResolvePath(args[1])
            : Path.Combine(CurrentDirectory, Path.GetFileNameWithoutExtension(archivePath));
        Directory.CreateDirectory(destination);
        string destinationRoot = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                return Error("unzip: blocked an unsafe path inside the archive");
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? destination);
            entry.ExtractToFile(target, true);
        }
        return Ok($"Extracted {archive.Entries.Count} entries to {destination}.");
    }

    private ShellResult ListZip(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("ziplist: missing archive path");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"ziplist: archive not found: {args[0]}");
        using ZipArchive archive = ZipFile.OpenRead(path);
        StringBuilder output = new();
        output.AppendLine($"{"SIZE",12}  MODIFIED             NAME");
        output.AppendLine(new string('─', 72));
        foreach (ZipArchiveEntry entry in archive.Entries.Take(1000))
            output.AppendLine($"{entry.Length,12:n0}  {entry.LastWriteTime:yyyy-MM-dd HH:mm}  {entry.FullName}");
        if (archive.Entries.Count > 1000)
            output.AppendLine("... output limited to 1,000 entries");
        return Ok(output.ToString().TrimEnd());
    }

    private ShellResult BackupPath(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("backup: missing path");
        string path = ResolvePath(args[0]);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        if (File.Exists(path))
        {
            string destination = path + ".backup-" + stamp;
            File.Copy(path, destination, false);
            return Ok(destination);
        }
        if (Directory.Exists(path))
        {
            string destination = path.TrimEnd(Path.DirectorySeparatorChar) + "-backup-" + stamp + ".zip";
            ZipFile.CreateFromDirectory(path, destination, CompressionLevel.Optimal, true);
            return Ok(destination);
        }
        return Error($"backup: path not found: {args[0]}");
    }

    private ShellResult PathInfoCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("pathinfo: missing path");
        string path = ResolvePath(args[0]);
        return Ok($"Input:      {args[0]}\nAbsolute:   {path}\nRoot:       {Path.GetPathRoot(path)}\nDirectory:  {Path.GetDirectoryName(path)}\nName:       {Path.GetFileName(path)}\nBase name:  {Path.GetFileNameWithoutExtension(path)}\nExtension:  {Path.GetExtension(path)}\nExists:     {File.Exists(path) || Directory.Exists(path)}\nKind:       {(Directory.Exists(path) ? "directory" : File.Exists(path) ? "file" : "missing")}");
    }

    private ShellResult RealPathCommand(string[] args, string raw) =>
        args.Length == 0 ? Error("realpath: missing path") : Ok(ResolvePath(args[0]));

    private ShellResult ExtensionCommand(string[] args, string raw) =>
        args.Length == 0 ? Error("extension: missing path") : Ok(Path.GetExtension(args[0]));

    private ShellResult CountFilesCommand(string[] args, string raw)
    {
        string path = args.Length == 0 ? CurrentDirectory : ResolvePath(args[0]);
        if (!Directory.Exists(path))
            return Error($"countfiles: directory not found: {path}");
        List<string> files = CollectFilesSafe(path, 100_000, out int fileSkips);
        List<string> directories = CollectDirectoriesSafe(path, 100_000, out int directorySkips);
        return Ok($"Files:       {files.Count:n0}\nDirectories: {directories.Count:n0}\nSkipped:     {fileSkips + directorySkips:n0}");
    }

    private ShellResult NewestItem(string[] args, string raw) => FindByTime(args, newest: true);

    private ShellResult OldestItem(string[] args, string raw) => FindByTime(args, newest: false);

    private ShellResult FindByTime(string[] args, bool newest)
    {
        string path = args.Length == 0 ? CurrentDirectory : ResolvePath(args[0]);
        if (!Directory.Exists(path))
            return Error($"{(newest ? "newest" : "oldest")}: directory not found: {path}");
        List<string> files = CollectFilesSafe(path, 50_000, out int fileSkips);
        List<string> directories = CollectDirectoriesSafe(path, 50_000, out int directorySkips);
        IEnumerable<string> items = files.Concat(directories);
        var result = items.Select(item => new { Path = item, Time = SafeWriteTime(item) })
            .Where(item => item.Time != DateTime.MinValue)
            .OrderBy(item => newest ? -item.Time.Ticks : item.Time.Ticks)
            .FirstOrDefault();
        return result is null ? Ok("No items found.") : Ok($"{result.Time:yyyy-MM-dd HH:mm:ss}  {result.Path}");
    }

    private ShellResult EmptyFiles(string[] args, string raw)
    {
        string path = args.Length == 0 ? CurrentDirectory : ResolvePath(args[0]);
        if (!Directory.Exists(path))
            return Error($"emptyfiles: directory not found: {path}");
        List<string> collected = CollectFilesSafe(path, 50_000, out int skipped);
        string[] files = collected.Where(file => SafeLength(file) == 0)
            .Take(1000)
            .ToArray();
        return Ok(files.Length == 0 ? "No empty files found." : string.Join(Environment.NewLine, files));
    }

    private ShellResult DuplicateFiles(string[] args, string raw)
    {
        string path = args.Length == 0 ? CurrentDirectory : ResolvePath(args[0]);
        if (!Directory.Exists(path))
            return Error($"duplicates: directory not found: {path}");
        string[] files = CollectFilesSafe(path, 5000, out int skipped).ToArray();
        var candidates = files.Select(file => new { File = file, Size = SafeLength(file) })
            .Where(item => item.Size >= 0)
            .GroupBy(item => item.Size)
            .Where(group => group.Count() > 1);

        List<IGrouping<string, string>> duplicates = new();
        foreach (var sizeGroup in candidates)
        {
            foreach (IGrouping<string, string> hashGroup in sizeGroup
                         .Select(item => new { item.File, Hash = TryHash(item.File) })
                         .Where(item => item.Hash is not null)
                         .GroupBy(item => item.Hash!, item => item.File)
                         .Where(group => group.Count() > 1))
                duplicates.Add(hashGroup);
        }

        if (duplicates.Count == 0)
            return Ok($"No duplicates found among {files.Length:n0} files. Skipped: {skipped:n0}");

        StringBuilder output = new();
        foreach (IGrouping<string, string> group in duplicates.Take(100))
        {
            output.AppendLine("SHA256 " + group.Key);
            foreach (string file in group)
                output.AppendLine("  " + file);
            output.AppendLine();
        }
        if (duplicates.Count > 100)
            output.AppendLine("... output limited to 100 duplicate groups");
        return Ok(output.ToString().TrimEnd());
    }

    private static List<string> CollectFilesSafe(string root, int limit, out int skipped)
    {
        List<string> results = new();
        Stack<string> pending = new();
        pending.Push(root);
        skipped = 0;

        while (pending.Count > 0 && results.Count < limit)
        {
            string current = pending.Pop();
            try
            {
                foreach (string file in Directory.EnumerateFiles(current))
                {
                    results.Add(file);
                    if (results.Count >= limit)
                        break;
                }

                if (results.Count >= limit)
                    break;

                foreach (string directory in Directory.EnumerateDirectories(current))
                    pending.Push(directory);
            }
            catch
            {
                skipped++;
            }
        }

        return results;
    }

    private static List<string> CollectDirectoriesSafe(string root, int limit, out int skipped)
    {
        List<string> results = new();
        Stack<string> pending = new();
        pending.Push(root);
        skipped = 0;

        while (pending.Count > 0 && results.Count < limit)
        {
            string current = pending.Pop();
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(current);
            }
            catch
            {
                skipped++;
                continue;
            }

            foreach (string directory in directories)
            {
                results.Add(directory);
                pending.Push(directory);
                if (results.Count >= limit)
                    break;
            }
        }

        return results;
    }

    private static string ComputeFileHash(string path, HashAlgorithm algorithm)
    {
        using (algorithm)
        using (FileStream stream = File.OpenRead(path))
            return Convert.ToHexString(algorithm.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string DetectMagic(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 })) return "PNG image";
        if (bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return "JPEG image";
        if (bytes.StartsWith(new byte[] { 0x47, 0x49, 0x46, 0x38 })) return "GIF image";
        if (bytes.StartsWith(new byte[] { 0x25, 0x50, 0x44, 0x46 })) return "PDF document";
        if (bytes.StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 })) return "ZIP-compatible archive";
        if (bytes.StartsWith(new byte[] { 0x4D, 0x5A })) return "Windows PE executable or DLL";
        if (bytes.StartsWith(new byte[] { 0x7F, 0x45, 0x4C, 0x46 })) return "ELF executable";
        if (bytes.StartsWith(new byte[] { 0x42, 0x4D })) return "BMP image";
        return "unknown / plain data";
    }

    private static int CountOccurrences(string text, string value)
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

    private static DateTime SafeWriteTime(string path)
    {
        try { return File.GetLastWriteTime(path); }
        catch { return DateTime.MinValue; }
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return -1; }
    }

    private static string? TryHash(string path)
    {
        try { return ComputeFileHash(path, SHA256.Create()); }
        catch { return null; }
    }
}
