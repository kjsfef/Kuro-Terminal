using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kuro;

public enum ToolInstallMethod
{
    GitHubAsset,
    Winget
}

public sealed record OpenSourceToolDefinition(
    string Name,
    string DisplayName,
    string Description,
    string License,
    string ProjectUrl,
    string ExecutableName,
    ToolInstallMethod InstallMethod,
    string? GitHubRepository = null,
    string[]? AssetTokens = null,
    string[]? AssetSuffixes = null,
    string? WingetId = null);

public readonly record struct ToolOperationResult(bool Success, string Output);

public static class OpenSourceToolManager
{
    private static readonly HttpClient Client = CreateClient();

    private static readonly Dictionary<string, OpenSourceToolDefinition> CatalogInternal =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["subfinder"] = new(
                "subfinder", "ProjectDiscovery Subfinder",
                "Passive subdomain discovery through public online sources.",
                "MIT", "https://github.com/projectdiscovery/subfinder", "subfinder.exe",
                ToolInstallMethod.GitHubAsset, "projectdiscovery/subfinder",
                ["windows", "amd64"], [".zip"]),
            ["amass"] = new(
                "amass", "OWASP Amass",
                "Open-source attack-surface mapping; Kuro exposes a passive-only wrapper.",
                "Apache-2.0", "https://github.com/owasp-amass/amass", "amass.exe",
                ToolInstallMethod.GitHubAsset, "owasp-amass/amass",
                ["windows", "amd64"], [".zip"]),
            ["gitleaks"] = new(
                "gitleaks", "Gitleaks",
                "Local secret scanner for repositories, files, and folders.",
                "MIT", "https://github.com/gitleaks/gitleaks", "gitleaks.exe",
                ToolInstallMethod.GitHubAsset, "gitleaks/gitleaks",
                ["windows", "x64"], [".zip"]),
            ["trivy"] = new(
                "trivy", "Aqua Trivy",
                "Local vulnerability, secret, and misconfiguration scanner.",
                "Apache-2.0", "https://github.com/aquasecurity/trivy", "trivy.exe",
                ToolInstallMethod.GitHubAsset, "aquasecurity/trivy",
                ["windows", "64bit"], [".zip"]),
            ["syft"] = new(
                "syft", "Anchore Syft",
                "Generate software bills of materials from local folders and images.",
                "Apache-2.0", "https://github.com/anchore/syft", "syft.exe",
                ToolInstallMethod.GitHubAsset, "anchore/syft",
                ["windows", "amd64"], [".zip"]),
            ["grype"] = new(
                "grype", "Anchore Grype",
                "Scan local filesystems and SBOMs for known vulnerabilities.",
                "Apache-2.0", "https://github.com/anchore/grype", "grype.exe",
                ToolInstallMethod.GitHubAsset, "anchore/grype",
                ["windows", "amd64"], [".zip"]),
            ["ripgrep"] = new(
                "ripgrep", "ripgrep",
                "Extremely fast recursive local text search.",
                "MIT / Unlicense", "https://github.com/BurntSushi/ripgrep", "rg.exe",
                ToolInstallMethod.GitHubAsset, "BurntSushi/ripgrep",
                ["x86_64-pc-windows-msvc"], [".zip"]),
            ["age"] = new(
                "age", "age",
                "Simple modern file encryption and key generation.",
                "BSD-3-Clause", "https://github.com/FiloSottile/age", "age.exe",
                ToolInstallMethod.GitHubAsset, "FiloSottile/age",
                ["windows", "amd64"], [".zip"]),
            ["jq"] = new(
                "jq", "jq",
                "Command-line JSON filtering and transformation.",
                "MIT", "https://github.com/jqlang/jq", "jq.exe",
                ToolInstallMethod.GitHubAsset, "jqlang/jq",
                ["windows", "amd64"], [".exe"]),
            ["exiftool"] = new(
                "exiftool", "ExifTool",
                "Read and remove metadata from images, media, and documents.",
                "Artistic-1.0 / GPL-1.0-or-later", "https://exiftool.org/", "exiftool.exe",
                ToolInstallMethod.Winget, WingetId: "OliverBetz.ExifTool")
        };

    public static IReadOnlyCollection<OpenSourceToolDefinition> Catalog =>
        CatalogInternal.Values.OrderBy(tool => tool.Name).ToArray();

    public static string ToolsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kuro", "Tools");

    public static OpenSourceToolDefinition? GetDefinition(string name) =>
        CatalogInternal.TryGetValue(name, out OpenSourceToolDefinition? tool) ? tool : null;

    public static string? FindExecutable(string toolName) =>
        FindToolFile(toolName, GetDefinition(toolName)?.ExecutableName ?? string.Empty);

    public static string? FindToolFile(string toolName, string fileName)
    {
        OpenSourceToolDefinition? definition = GetDefinition(toolName);
        if (definition is null || string.IsNullOrWhiteSpace(fileName))
            return null;

        string localDirectory = Path.Combine(ToolsDirectory, definition.Name);
        if (Directory.Exists(localDirectory))
        {
            string? localPath = Directory.GetFiles(localDirectory, fileName, SearchOption.AllDirectories)
                .FirstOrDefault();
            if (localPath is not null)
                return localPath;
        }

        return FindOnPath(fileName);
    }

    public static ToolOperationResult Install(string toolName)
    {
        OpenSourceToolDefinition? definition = GetDefinition(toolName);
        if (definition is null)
            return new ToolOperationResult(false, $"Unknown tool: {toolName}");

        try
        {
            Directory.CreateDirectory(ToolsDirectory);
            return definition.InstallMethod switch
            {
                ToolInstallMethod.GitHubAsset => InstallFromGitHub(definition),
                ToolInstallMethod.Winget => InstallWithWinget(definition),
                _ => new ToolOperationResult(false, "Unsupported install method.")
            };
        }
        catch (Exception ex)
        {
            return new ToolOperationResult(false, $"Failed to install {definition.Name}: {ex.Message}");
        }
    }

    public static ToolOperationResult Remove(string toolName)
    {
        OpenSourceToolDefinition? definition = GetDefinition(toolName);
        if (definition is null)
            return new ToolOperationResult(false, $"Unknown tool: {toolName}");

        try
        {
            string localDirectory = Path.Combine(ToolsDirectory, definition.Name);
            if (Directory.Exists(localDirectory))
            {
                Directory.Delete(localDirectory, true);
                return new ToolOperationResult(true, $"Removed Kuro's local {definition.Name} installation.");
            }

            if (definition.InstallMethod == ToolInstallMethod.Winget && !string.IsNullOrWhiteSpace(definition.WingetId))
            {
                return RunProcess("winget", ["uninstall", "--id", definition.WingetId, "-e", "--silent"],
                    Environment.CurrentDirectory, 180_000);
            }

            return new ToolOperationResult(false, $"{definition.Name} is not installed by Kuro.");
        }
        catch (Exception ex)
        {
            return new ToolOperationResult(false, $"Failed to remove {definition.Name}: {ex.Message}");
        }
    }

    public static ToolOperationResult Run(
        string toolName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int timeoutMilliseconds = 120_000)
    {
        string? executable = FindExecutable(toolName);
        if (executable is null)
            return new ToolOperationResult(false, $"{toolName} is not installed. Run: tools install {toolName}");

        return RunProcess(executable, arguments, workingDirectory, timeoutMilliseconds);
    }

    public static ToolOperationResult RunCompanion(
        string toolName,
        string executableName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int timeoutMilliseconds = 120_000)
    {
        string? executable = FindToolFile(toolName, executableName);
        if (executable is null)
            return new ToolOperationResult(false,
                $"{executableName} is not installed. Run: tools install {toolName}");
        return RunProcess(executable, arguments, workingDirectory, timeoutMilliseconds);
    }

    private static ToolOperationResult InstallFromGitHub(OpenSourceToolDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.GitHubRepository))
            return new ToolOperationResult(false, "Tool repository is missing.");

        string apiUrl = $"https://api.github.com/repos/{definition.GitHubRepository}/releases/latest";
        string json = Client.GetStringAsync(apiUrl).GetAwaiter().GetResult();
        using JsonDocument document = JsonDocument.Parse(json);

        string tag = document.RootElement.TryGetProperty("tag_name", out JsonElement tagElement)
            ? tagElement.GetString() ?? "latest"
            : "latest";

        JsonElement? match = null;
        foreach (JsonElement asset in document.RootElement.GetProperty("assets").EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString() ?? string.Empty;
            bool tokenMatch = definition.AssetTokens is null ||
                              definition.AssetTokens.All(token =>
                                  name.Contains(token, StringComparison.OrdinalIgnoreCase));
            bool suffixMatch = definition.AssetSuffixes is null ||
                               definition.AssetSuffixes.Any(suffix =>
                                   name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (tokenMatch && suffixMatch)
            {
                match = asset;
                break;
            }
        }

        if (match is null)
            return new ToolOperationResult(false,
                $"No compatible Windows x64 release asset was found for {definition.Name}.");

        string assetName = match.Value.GetProperty("name").GetString() ?? definition.ExecutableName;
        string downloadUrl = match.Value.GetProperty("browser_download_url").GetString()
                             ?? throw new InvalidOperationException("Release asset URL is missing.");

        string tempRoot = Path.Combine(Path.GetTempPath(), "KuroTools", Guid.NewGuid().ToString("N"));
        string downloadPath = Path.Combine(tempRoot, assetName);
        string extractPath = Path.Combine(tempRoot, "extract");
        Directory.CreateDirectory(tempRoot);

        try
        {
            byte[] payload = Client.GetByteArrayAsync(downloadUrl).GetAwaiter().GetResult();
            File.WriteAllBytes(downloadPath, payload);
            string sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

            string installDirectory = Path.Combine(ToolsDirectory, definition.Name);
            if (Directory.Exists(installDirectory))
                Directory.Delete(installDirectory, true);
            Directory.CreateDirectory(installDirectory);

            if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(extractPath);
                ZipFile.ExtractToDirectory(downloadPath, extractPath, true);
                CopyDirectory(extractPath, installDirectory);
            }
            else
            {
                File.Copy(downloadPath, Path.Combine(installDirectory, definition.ExecutableName), true);
            }

            string? executable = Directory.GetFiles(installDirectory, definition.ExecutableName,
                SearchOption.AllDirectories).FirstOrDefault();
            if (executable is null)
                return new ToolOperationResult(false,
                    $"Downloaded release did not contain {definition.ExecutableName}.");

            File.WriteAllText(Path.Combine(installDirectory, "kuro-tool.json"),
                JsonSerializer.Serialize(new
                {
                    definition.Name,
                    definition.DisplayName,
                    Version = tag,
                    Source = definition.ProjectUrl,
                    Repository = definition.GitHubRepository,
                    Asset = assetName,
                    Sha256 = sha256,
                    definition.License,
                    InstalledAtUtc = DateTime.UtcNow
                }, new JsonSerializerOptions { WriteIndented = true }));

            return new ToolOperationResult(true,
                $"Installed {definition.DisplayName} {tag}\n" +
                $"Location: {installDirectory}\nSHA-256:  {sha256}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
            catch
            {
                // Temporary cleanup is best effort.
            }
        }
    }

    private static ToolOperationResult InstallWithWinget(OpenSourceToolDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.WingetId))
            return new ToolOperationResult(false, "WinGet package ID is missing.");

        ToolOperationResult result = RunProcess("winget",
            ["install", "--id", definition.WingetId, "-e", "--silent",
                "--accept-package-agreements", "--accept-source-agreements"],
            Environment.CurrentDirectory, 300_000);

        if (!result.Success)
            return result;

        return new ToolOperationResult(true,
            result.Output + Environment.NewLine +
            $"{definition.DisplayName} was installed with WinGet. Restart Kuro if PATH has not refreshed yet.");
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, file);
            string destination = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }

    private static ToolOperationResult RunProcess(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int timeoutMilliseconds)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            WorkingDirectory = Directory.Exists(workingDirectory)
                ? workingDirectory
                : Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new ToolOperationResult(false, $"Could not start {executable}: {ex.Message}");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMilliseconds))
        {
            try { process.Kill(true); } catch { }
            return new ToolOperationResult(false,
                $"{Path.GetFileName(executable)} exceeded the time limit and was stopped.");
        }

        string stdout = outputTask.GetAwaiter().GetResult().TrimEnd();
        string stderr = errorTask.GetAwaiter().GetResult().TrimEnd();
        StringBuilder combined = new();
        if (!string.IsNullOrWhiteSpace(stdout))
            combined.AppendLine(stdout);
        if (!string.IsNullOrWhiteSpace(stderr))
            combined.AppendLine(stderr);

        string text = combined.ToString().TrimEnd();
        if (string.IsNullOrWhiteSpace(text))
            text = process.ExitCode == 0 ? "Completed successfully." : $"Exited with code {process.ExitCode}.";

        return new ToolOperationResult(process.ExitCode == 0, text);
    }

    private static string? FindOnPath(string executableName)
    {
        if (File.Exists(executableName))
            return Path.GetFullPath(executableName);

        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(directory.Trim('"'), executableName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }

    private static HttpClient CreateClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Kuro-Terminal/1.1");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
