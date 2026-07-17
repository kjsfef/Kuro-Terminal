using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Kuro;

public sealed partial class ShellEngine
{
    private sealed record CommunityTool(
        string Name,
        string Executable,
        string Category,
        string ProjectUrl,
        string InstallHint,
        string Description);

    private static readonly IReadOnlyDictionary<string, CommunityTool> CommunityTools =
        MegaBuildCommunityToolCatalog();

    private void RegisterCommunityToolHubCommands()
    {
        Register("toolhub", "Open Source", "toolhub [osint|opsec|local]", "Show Kuro's curated open-source research and privacy tool hub.", ToolHubCommand);
        Register("toolstatus", "Open Source", "toolstatus [name]", "Check whether curated open-source tools are installed.", ToolStatusCommand);
        Register("toolopen", "Open Source", "toolopen <name>", "Open a curated tool's official project page.", ToolOpenCommand);
        Register("toolsetup", "Open Source", "toolsetup <name>", "Show a tool's official installation hint.", ToolSetupCommand);
        Register("wingetsearch", "Open Source", "wingetsearch <query>", "Search Windows Package Manager for a package.", WingetSearchCommand);

        Register("sherlock", "Social OSINT", "sherlock <username> [extra args]", "Run the installed open-source Sherlock username finder.", (args, raw) => RunCommunityTool("sherlock", args));
        Register("maigret", "Social OSINT", "maigret <username> [extra args]", "Run the installed open-source Maigret username finder.", (args, raw) => RunCommunityTool("maigret", args));
        Register("spiderfoot", "OSINT Platform", "spiderfoot [args]", "Run an installed SpiderFoot command-line entry point.", (args, raw) => RunCommunityTool("spiderfoot", args));
        Register("theharvester", "Domain OSINT", "theharvester [args]", "Run an installed theHarvester command-line entry point.", (args, raw) => RunCommunityTool("theharvester", args));
        Register("trufflehog", "Local Security", "trufflehog [args]", "Run an installed TruffleHog secret scanner.", (args, raw) => RunCommunityTool("trufflehog", args));
        Register("yara", "Local Security", "yara [args]", "Run an installed YARA scanner.", (args, raw) => RunCommunityTool("yara", args));
        Register("mat2", "Privacy", "mat2 [args]", "Run an installed MAT2 metadata cleaner.", (args, raw) => RunCommunityTool("mat2", args));

        Register("opsec", "Privacy", "opsec", "Show practical defensive privacy and account-safety guidance.", OpsecOverviewCommand);
    }

    private ShellResult ToolHubCommand(string[] args, string raw)
    {
        string? filter = args.Length > 0 ? args[0] : null;
        IEnumerable<CommunityTool> tools = CommunityTools.Values
            .OrderBy(tool => tool.Category)
            .ThenBy(tool => tool.Name);

        if (!string.IsNullOrWhiteSpace(filter))
            tools = tools.Where(tool =>
                tool.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                tool.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

        CommunityTool[] selected = tools.ToArray();
        if (selected.Length == 0)
            return Error("toolhub: no tools matched that filter");

        StringBuilder output = new();
        output.AppendLine("KURO OPEN-SOURCE TOOL HUB");
        output.AppendLine("────────────────────────────────────────────────────────────────────────");

        foreach (IGrouping<string, CommunityTool> group in selected.GroupBy(tool => tool.Category))
        {
            output.AppendLine();
            output.AppendLine(group.Key.ToUpperInvariant());
            foreach (CommunityTool tool in group)
            {
                string state = FindExecutable(tool.Executable) is null ? "not found" : "installed";
                output.AppendLine($"  {tool.Name,-14} [{state,-9}] {tool.Description}");
            }
        }

        output.AppendLine();
        output.AppendLine("Use: toolstatus <name> | toolsetup <name> | toolopen <name>");
        output.AppendLine("Kuro only launches tools you explicitly install and run.");

        return Ok(output.ToString().TrimEnd());
    }

    private ShellResult ToolStatusCommand(string[] args, string raw)
    {
        IEnumerable<CommunityTool> tools = CommunityTools.Values;
        if (args.Length > 0)
        {
            if (!CommunityTools.TryGetValue(args[0], out CommunityTool? selected))
                return Error("toolstatus: unknown tool; run toolhub");
            tools = new[] { selected };
        }

        return Ok(string.Join(
            Environment.NewLine,
            tools.OrderBy(tool => tool.Name).Select(tool =>
            {
                string? executable = FindExecutable(tool.Executable);
                return executable is null
                    ? $"{tool.Name,-14} not installed"
                    : $"{tool.Name,-14} {executable}";
            })));
    }

    private ShellResult ToolOpenCommand(string[] args, string raw)
    {
        if (args.Length == 0 || !CommunityTools.TryGetValue(args[0], out CommunityTool? tool))
            return Error("toolopen: use toolopen <name>; run toolhub for names");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = tool.ProjectUrl,
                UseShellExecute = true
            });
            return Ok($"opened official project: {tool.ProjectUrl}");
        }
        catch (Exception ex)
        {
            return Error($"toolopen: {ex.Message}");
        }
    }

    private ShellResult ToolSetupCommand(string[] args, string raw)
    {
        if (args.Length == 0 || !CommunityTools.TryGetValue(args[0], out CommunityTool? tool))
            return Error("toolsetup: use toolsetup <name>; run toolhub for names");

        return Ok(
            $"{tool.Name} — {tool.Description}\n" +
            $"Official project: {tool.ProjectUrl}\n" +
            $"Install hint: {tool.InstallHint}\n\n" +
            "Review the upstream documentation before installing or running any third-party tool.");
    }

    private ShellResult WingetSearchCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("wingetsearch: enter a package name");

        string? winget = FindExecutable("winget");
        if (winget is null)
            return Error("wingetsearch: Windows Package Manager was not found");

        return RunExternalExplicit(
            new[] { winget, "search", string.Join(' ', args) });
    }

    private ShellResult RunCommunityTool(string name, string[] args)
    {
        if (!CommunityTools.TryGetValue(name, out CommunityTool? tool))
            return Error($"tool: unknown tool: {name}");

        string? executable = FindExecutable(tool.Executable);
        if (executable is null)
        {
            return Error(
                $"{name}: not installed or not in PATH\n" +
                $"Official project: {tool.ProjectUrl}\n" +
                $"Setup: {tool.InstallHint}\n" +
                $"Run: toolsetup {name}");
        }

        List<string> tokens = new() { executable };
        tokens.AddRange(args);
        return RunExternalExplicit(tokens.ToArray());
    }

    private ShellResult OpsecOverviewCommand(string[] args, string raw) =>
        Ok(
            "KURO DEFENSIVE OPSEC CHECKLIST\n" +
            "────────────────────────────────────────────────────────\n" +
            "1. Use unique passwords and a password manager.\n" +
            "2. Prefer passkeys or hardware-backed MFA; save recovery codes offline.\n" +
            "3. Separate public identities, email aliases, browsers, and profiles by purpose.\n" +
            "4. Remove document/photo metadata before publishing.\n" +
            "5. Encrypt sensitive local data and keep tested backups.\n" +
            "6. Review account sessions, OAuth grants, forwarding rules, and recovery methods.\n" +
            "7. Keep Windows, browsers, Kuro, and security tools updated.\n" +
            "8. Treat VPNs and Tor as transport tools, not magic anonymity.\n\n" +
            "Useful Kuro commands: password, passphrase, pwnedpass, metadata, metaclean,\n" +
            "hash, secretscan, trivyfs, toolhub opsec, toolstatus.");

    private static IReadOnlyDictionary<string, CommunityTool> MegaBuildCommunityToolCatalog()
    {
        CommunityTool[] tools =
        {
            new(
                "sherlock",
                "sherlock",
                "Social OSINT",
                "https://github.com/sherlock-project/sherlock",
                "Install pipx, then run: pipx install sherlock-project",
                "Public username discovery across supported sites."),
            new(
                "maigret",
                "maigret",
                "Social OSINT",
                "https://github.com/soxoj/maigret",
                "Install pipx, then run: pipx install maigret",
                "Public username research across many sites."),
            new(
                "spiderfoot",
                "sf.py",
                "OSINT Platform",
                "https://github.com/smicallef/spiderfoot",
                "Follow the official GitHub installation instructions for Python 3.",
                "Modular OSINT automation and public-source correlation."),
            new(
                "theharvester",
                "theHarvester",
                "Domain OSINT",
                "https://github.com/laramies/theHarvester",
                "Follow the official repository installation instructions.",
                "Public-source domain, host, and email discovery."),
            new(
                "trufflehog",
                "trufflehog",
                "Local Security",
                "https://github.com/trufflesecurity/trufflehog",
                "Download a signed release from the official GitHub repository.",
                "Detect exposed secrets in authorized local repositories."),
            new(
                "yara",
                "yara64",
                "Local Security",
                "https://github.com/VirusTotal/yara",
                "Use an official YARA release or a trusted package manager.",
                "Rule-based local file classification and scanning."),
            new(
                "mat2",
                "mat2",
                "Privacy",
                "https://0xacab.org/jvoisin/mat2",
                "Install MAT2 using its official platform instructions.",
                "Remove metadata from supported local file formats."),
            new(
                "keepassxc",
                "KeePassXC",
                "OPSEC",
                "https://github.com/keepassxreboot/keepassxc",
                "Search Windows Package Manager: wingetsearch KeePassXC",
                "Open-source offline password manager."),
            new(
                "veracrypt",
                "VeraCrypt",
                "OPSEC",
                "https://github.com/veracrypt/VeraCrypt",
                "Search Windows Package Manager: wingetsearch VeraCrypt",
                "Open-source encrypted volumes and containers."),
            new(
                "cryptomator",
                "Cryptomator",
                "OPSEC",
                "https://github.com/cryptomator/cryptomator",
                "Search Windows Package Manager: wingetsearch Cryptomator",
                "Client-side encryption for cloud-synced folders."),
            new(
                "onionshare",
                "onionshare",
                "OPSEC",
                "https://github.com/onionshare/onionshare",
                "Use the official OnionShare downloads or package instructions.",
                "Open-source Tor-based file sharing and services."),
            new(
                "bleachbit",
                "bleachbit",
                "OPSEC",
                "https://github.com/bleachbit/bleachbit",
                "Search Windows Package Manager: wingetsearch BleachBit",
                "Open-source local cleanup and data-removal utility.")
        };

        return tools.ToDictionary(
            tool => tool.Name,
            StringComparer.OrdinalIgnoreCase);
    }
}
