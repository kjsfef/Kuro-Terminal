using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Kuro;

public sealed partial class ShellEngine
{
    private static readonly Regex DomainPattern = new(
        @"^(?=.{1,253}$)(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,63}$",
        RegexOptions.Compiled);

    private void RegisterOpenSourceToolCommands()
    {
        Register("tools", "Open Source", "tools [list|install|update|remove|info|licenses] [name|all]",
            "Manage Kuro's integrated open-source tools.", ToolsCommand, "toolbox", "tool");
        Register("subfinder", "Open Source", "subfinder <domain> [extra arguments]",
            "Run ProjectDiscovery Subfinder for passive subdomain discovery.", SubfinderCommand,
            "subdomains", "passive-subdomains");
        Register("amasspassive", "Open Source", "amasspassive <domain> [extra arguments]",
            "Run OWASP Amass in passive-only mode.", AmassPassiveCommand, "amass-osint");
        Register("metadata", "Open Source", "metadata <file> [extra arguments]",
            "Read file metadata with ExifTool.", MetadataCommand, "exif");
        Register("metaclean", "Open Source", "metaclean <file> [output-file]",
            "Create a metadata-stripped copy with ExifTool.", MetadataCleanCommand);
        Register("secretscan", "Open Source", "secretscan [path]",
            "Scan local files and folders for exposed secrets with Gitleaks.", SecretScanCommand,
            "leakscan");
        Register("gitsecretscan", "Open Source", "gitsecretscan [repository]",
            "Scan local Git history for exposed secrets with Gitleaks.", GitSecretScanCommand);
        Register("trivyfs", "Open Source", "trivyfs [path]",
            "Scan a local folder for vulnerabilities, secrets, and misconfigurations with Trivy.", TrivyFileSystemCommand,
            "trivyscan");
        Register("sbom", "Open Source", "sbom [path] [output.json]",
            "Generate a CycloneDX JSON SBOM with Syft.", SbomCommand);
        Register("vulnscan", "Open Source", "vulnscan [path]",
            "Scan a local folder for known vulnerabilities with Grype.", VulnerabilityScanCommand,
            "grypescan");
        Register("fastgrep", "Open Source", "fastgrep <pattern> [path]",
            "Search local files recursively with ripgrep.", FastGrepCommand, "rg");
        Register("jsonq", "Open Source", "jsonq <filter> <json-file>",
            "Filter a local JSON file with jq.", JsonQueryCommand);
        Register("agekey", "Open Source", "agekey [identity-file]",
            "Generate an age identity and print its public recipient.", AgeKeyCommand);
        Register("ageencrypt", "Open Source", "ageencrypt <file> <recipient> [output.age]",
            "Encrypt a local file with age.", AgeEncryptCommand);
        Register("agedecrypt", "Open Source", "agedecrypt <file.age> <identity-file> [output]",
            "Decrypt a local age file.", AgeDecryptCommand);
    }

    private ShellResult ToolsCommand(string[] args, string raw)
    {
        string action = args.Length == 0 ? "list" : args[0].ToLowerInvariant();
        if (action is "list" or "status")
        {
            StringBuilder output = new();
            output.AppendLine("KURO OPEN-SOURCE TOOL HUB");
            foreach (OpenSourceToolDefinition tool in OpenSourceToolManager.Catalog)
            {
                string status = OpenSourceToolManager.FindExecutable(tool.Name) is null
                    ? "not installed"
                    : "installed";
                output.AppendLine($"  {tool.Name,-11} {status,-13} {tool.Description}");
            }
            output.AppendLine();
            output.AppendLine("Install everything: tools install all");
            output.Append($"Local tool folder: {OpenSourceToolManager.ToolsDirectory}");
            return Ok(output.ToString());
        }

        if (action == "licenses")
        {
            return Ok(string.Join(Environment.NewLine,
                OpenSourceToolManager.Catalog.Select(tool =>
                    $"{tool.Name,-11} {tool.License,-30} {tool.ProjectUrl}")));
        }

        if (action == "info")
        {
            if (args.Length < 2)
                return Error("tools info: provide a tool name");
            OpenSourceToolDefinition? tool = OpenSourceToolManager.GetDefinition(args[1]);
            if (tool is null)
                return Error($"Unknown tool: {args[1]}");
            string status = OpenSourceToolManager.FindExecutable(tool.Name) ?? "not installed";
            return Ok($"{tool.DisplayName}\n" +
                      $"  purpose: {tool.Description}\n" +
                      $"  license: {tool.License}\n" +
                      $"  source:  {tool.ProjectUrl}\n" +
                      $"  status:  {status}");
        }

        if (action is "install" or "update")
        {
            if (args.Length < 2)
                return Error($"tools {action}: provide a tool name or all");

            string target = args[1];
            if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                StringBuilder output = new();
                bool allSucceeded = true;
                foreach (OpenSourceToolDefinition tool in OpenSourceToolManager.Catalog)
                {
                    ToolOperationResult result = OpenSourceToolManager.Install(tool.Name);
                    allSucceeded &= result.Success;
                    output.AppendLine($"[{(result.Success ? "ok" : "error")}] {tool.Name}: {result.Output}");
                }
                return allSucceeded ? Ok(output.ToString().TrimEnd()) : Error(output.ToString().TrimEnd());
            }

            ToolOperationResult single = OpenSourceToolManager.Install(target);
            return single.Success ? Ok(single.Output) : Error(single.Output);
        }

        if (action is "remove" or "uninstall")
        {
            if (args.Length < 2)
                return Error("tools remove: provide a tool name");
            ToolOperationResult result = OpenSourceToolManager.Remove(args[1]);
            return result.Success ? Ok(result.Output) : Error(result.Output);
        }

        return Error("tools: use list, info, licenses, install, update, or remove");
    }

    private ShellResult SubfinderCommand(string[] args, string raw)
    {
        if (!TryGetDomain(args, "subfinder", out string domain, out ShellResult error))
            return error;

        string[] toolArgs = ["-silent", "-d", domain, .. args.Skip(1)];
        return ToolResult(OpenSourceToolManager.Run("subfinder", toolArgs, CurrentDirectory, 180_000));
    }

    private ShellResult AmassPassiveCommand(string[] args, string raw)
    {
        if (!TryGetDomain(args, "amasspassive", out string domain, out ShellResult error))
            return error;

        string[] toolArgs = ["enum", "-passive", "-d", domain, .. args.Skip(1)];
        return ToolResult(OpenSourceToolManager.Run("amass", toolArgs, CurrentDirectory, 300_000));
    }

    private bool TryGetDomain(string[] args, string command, out string domain, out ShellResult error)
    {
        domain = args.Length == 0 ? string.Empty : args[0].Trim().ToLowerInvariant();
        if (args.Length == 0)
        {
            error = Error($"{command}: provide a domain, for example {command} example.com");
            return false;
        }
        if (!DomainPattern.IsMatch(domain))
        {
            error = Error($"{command}: enter a domain such as example.com, not a full URL");
            return false;
        }
        error = ShellResult.Empty;
        return true;
    }

    private ShellResult MetadataCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("metadata: provide a file path");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"metadata: file not found: {path}");

        string[] toolArgs = ["-G1", "-a", "-s", path, .. args.Skip(1)];
        return ToolResult(OpenSourceToolManager.Run("exiftool", toolArgs, CurrentDirectory));
    }

    private ShellResult MetadataCleanCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("metaclean: provide a file path");
        string input = ResolvePath(args[0]);
        if (!File.Exists(input))
            return Error($"metaclean: file not found: {input}");

        string output = args.Length >= 2
            ? ResolvePath(args[1])
            : Path.Combine(Path.GetDirectoryName(input) ?? CurrentDirectory,
                $"{Path.GetFileNameWithoutExtension(input)}.clean{Path.GetExtension(input)}");

        ToolOperationResult result = OpenSourceToolManager.Run("exiftool",
            ["-all=", "-o", output, input], CurrentDirectory);
        return result.Success
            ? Ok(result.Output + Environment.NewLine + $"Clean copy: {output}")
            : Error(result.Output);
    }

    private ShellResult SecretScanCommand(string[] args, string raw)
    {
        string target = args.Length == 0 ? CurrentDirectory : ResolvePath(args[0]);
        if (!File.Exists(target) && !Directory.Exists(target))
            return Error($"secretscan: path not found: {target}");

        return ToolResult(OpenSourceToolManager.Run("gitleaks",
            ["dir", "--no-banner", "--no-color", "--redact", "--exit-code", "0", target],
            CurrentDirectory, 300_000));
    }

    private ShellResult GitSecretScanCommand(string[] args, string raw)
    {
        string target = args.Length == 0 ? CurrentDirectory : ResolvePath(args[0]);
        if (!Directory.Exists(target))
            return Error($"gitsecretscan: directory not found: {target}");
        if (!Directory.Exists(Path.Combine(target, ".git")))
            return Error("gitsecretscan: target is not a local Git repository");

        return ToolResult(OpenSourceToolManager.Run("gitleaks",
            ["git", "--no-banner", "--no-color", "--redact", "--exit-code", "0", target],
            CurrentDirectory, 300_000));
    }

    private ShellResult TrivyFileSystemCommand(string[] args, string raw)
    {
        string target = args.Length == 0 ? CurrentDirectory : ResolvePath(args[0]);
        if (!File.Exists(target) && !Directory.Exists(target))
            return Error($"trivyfs: path not found: {target}");

        return ToolResult(OpenSourceToolManager.Run("trivy",
            ["fs", "--no-progress", "--scanners", "vuln,secret,misconfig", target],
            CurrentDirectory, 600_000));
    }

    private ShellResult SbomCommand(string[] args, string raw)
    {
        string target = args.Length == 0 ? CurrentDirectory : ResolvePath(args[0]);
        if (!File.Exists(target) && !Directory.Exists(target))
            return Error($"sbom: path not found: {target}");

        string output = args.Length >= 2
            ? ResolvePath(args[1])
            : Path.Combine(CurrentDirectory, "kuro-sbom.cdx.json");
        ToolOperationResult result = OpenSourceToolManager.Run("syft",
            ["scan", target, "-o", $"cyclonedx-json={output}"], CurrentDirectory, 600_000);
        return result.Success ? Ok(result.Output + Environment.NewLine + $"SBOM: {output}") : Error(result.Output);
    }

    private ShellResult VulnerabilityScanCommand(string[] args, string raw)
    {
        string target = args.Length == 0 ? CurrentDirectory : ResolvePath(args[0]);
        if (!File.Exists(target) && !Directory.Exists(target))
            return Error($"vulnscan: path not found: {target}");
        string source = Directory.Exists(target) ? "dir:" + target : target;
        return ToolResult(OpenSourceToolManager.Run("grype",
            [source, "--output", "table"], CurrentDirectory, 600_000));
    }

    private ShellResult FastGrepCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("fastgrep: provide a search pattern");
        string target = args.Length >= 2 ? ResolvePath(args[1]) : CurrentDirectory;
        if (!File.Exists(target) && !Directory.Exists(target))
            return Error($"fastgrep: path not found: {target}");
        return ToolResult(OpenSourceToolManager.Run("ripgrep",
            ["--line-number", "--hidden", "--glob", "!.git/*", args[0], target],
            CurrentDirectory, 300_000));
    }

    private ShellResult JsonQueryCommand(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("jsonq: use jsonq <filter> <json-file>");
        string file = ResolvePath(args[1]);
        if (!File.Exists(file))
            return Error($"jsonq: file not found: {file}");
        return ToolResult(OpenSourceToolManager.Run("jq", [args[0], file], CurrentDirectory));
    }

    private ShellResult AgeKeyCommand(string[] args, string raw)
    {
        string output = args.Length == 0
            ? Path.Combine(CurrentDirectory, "kuro-age-key.txt")
            : ResolvePath(args[0]);
        ToolOperationResult result = OpenSourceToolManager.RunCompanion("age", "age-keygen.exe",
            ["-o", output], CurrentDirectory);
        return result.Success ? Ok(result.Output + Environment.NewLine + $"Identity: {output}") : Error(result.Output);
    }

    private ShellResult AgeEncryptCommand(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("ageencrypt: use ageencrypt <file> <recipient> [output.age]");
        string input = ResolvePath(args[0]);
        if (!File.Exists(input))
            return Error($"ageencrypt: file not found: {input}");
        string output = args.Length >= 3 ? ResolvePath(args[2]) : input + ".age";
        ToolOperationResult result = OpenSourceToolManager.Run("age",
            ["-r", args[1], "-o", output, input], CurrentDirectory);
        return result.Success ? Ok(result.Output + Environment.NewLine + $"Encrypted: {output}") : Error(result.Output);
    }

    private ShellResult AgeDecryptCommand(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("agedecrypt: use agedecrypt <file.age> <identity-file> [output]");
        string input = ResolvePath(args[0]);
        string identity = ResolvePath(args[1]);
        if (!File.Exists(input) || !File.Exists(identity))
            return Error("agedecrypt: encrypted file or identity file was not found");
        string output = args.Length >= 3
            ? ResolvePath(args[2])
            : Path.Combine(Path.GetDirectoryName(input) ?? CurrentDirectory,
                Path.GetFileNameWithoutExtension(input));
        ToolOperationResult result = OpenSourceToolManager.Run("age",
            ["-d", "-i", identity, "-o", output, input], CurrentDirectory);
        return result.Success ? Ok(result.Output + Environment.NewLine + $"Decrypted: {output}") : Error(result.Output);
    }

    private ShellResult ToolResult(ToolOperationResult result) =>
        result.Success ? Ok(result.Output) : Error(result.Output);
}
