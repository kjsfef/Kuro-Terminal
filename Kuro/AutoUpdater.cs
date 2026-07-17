using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace Kuro;

public readonly record struct UpdateAttemptResult(bool Success, string Message);

public static class AutoUpdater
{
    private const string RepositoryMetadataKey = "KuroRepositoryUrl";

    public static string RepositoryUrl
    {
        get
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            return assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                       .FirstOrDefault(attribute =>
                           string.Equals(attribute.Key, RepositoryMetadataKey,
                               StringComparison.OrdinalIgnoreCase))?.Value?.Trim()
                   ?? string.Empty;
        }
    }

    public static bool IsConfigured
    {
        get
        {
            if (RepositoryUrl.Contains("REPLACE_ME", StringComparison.OrdinalIgnoreCase))
                return false;

            return Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out Uri? uri) &&
                   string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
                   uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 2;
        }
    }

    public static async Task<UpdateAttemptResult> CheckAndApplyAsync()
    {
        if (!IsConfigured)
        {
            return new UpdateAttemptResult(false,
                "Auto-update is not configured in this local build. Publish Kuro through the included GitHub Actions workflow.");
        }

        try
        {
            GithubSource source = new(RepositoryUrl, null, false);
            UpdateManager manager = new(source);

            if (!manager.IsInstalled)
            {
                return new UpdateAttemptResult(false,
                    "This copy is running from Visual Studio or a normal folder. " +
                    "Install Kuro using Kuro-Setup.exe from GitHub Releases to enable automatic updates.");
            }

            UpdateInfo? update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is null)
                return new UpdateAttemptResult(true, "Kuro is already up to date.");

            await manager.DownloadUpdatesAsync(update).ConfigureAwait(false);
            manager.ApplyUpdatesAndRestart(update);
            return new UpdateAttemptResult(true, "Update downloaded. Restarting Kuro...");
        }
        catch (Exception ex)
        {
            return new UpdateAttemptResult(false, $"Update check failed: {ex.Message}");
        }
    }
}
