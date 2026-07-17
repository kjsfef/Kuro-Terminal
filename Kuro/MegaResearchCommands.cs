using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Kuro;

public sealed partial class ShellEngine
{
    private static readonly HttpClient MegaOsintHttp = CreateMegaOsintClient();

    private void RegisterMegaResearchCommands()
    {
        Register("osint", "OSINT+", "osint", "Show Kuro's passive public-data research command map.", OsintOverviewCommand);
        Register("breachcheck", "OSINT+", "breachcheck <email>", "Check an authorized email through Have I Been Pwned. Requires HIBP_API_KEY.", BreachCheckCommand);
        Register("pwnedpass", "Defensive", "pwnedpass <password>", "Check a password with HIBP's k-anonymity range API; the password is never sent.", PwnedPasswordCommand);
        Register("emailosint", "OSINT+", "emailosint <email>", "Build a passive email/domain research summary without probing the mailbox.", EmailOsintCommand);
        Register("gravatar", "OSINT+", "gravatar <email>", "Generate the public Gravatar profile and avatar URLs for an email hash.", GravatarCommand);
        Register("instagram", "Social OSINT", "instagram <username>", "Open a public Instagram profile URL.", InstagramCommand, "ig");
        Register("sociallinks", "Social OSINT", "sociallinks <username> [--open]", "Generate public profile links across major platforms.", SocialLinksCommand);
        Register("githubuser", "Social OSINT", "githubuser <username>", "Read a GitHub user's public profile through GitHub's public API.", GitHubUserCommand);
        Register("crtsh", "OSINT+", "crtsh <domain>", "List public certificate-transparency names from crt.sh.", CrtShCommand);
        Register("wayback", "OSINT+", "wayback <url>", "Open the Internet Archive history for a URL.", WaybackCommand);
        Register("archiveurl", "OSINT+", "archiveurl <url>", "Open the Internet Archive save page for a public URL.", ArchiveUrlCommand);
        Register("searchweb", "OSINT+", "searchweb <query>", "Open a web search for the supplied research query.", SearchWebCommand);
        Register("whoisweb", "OSINT+", "whoisweb <domain>", "Open ICANN's public registration lookup.", WhoIsWebCommand);
        Register("urlscanweb", "OSINT+", "urlscanweb <domain|url>", "Open a public urlscan.io search.", UrlScanWebCommand);
        Register("virustotalweb", "Defensive", "virustotalweb <hash|domain|url>", "Open a VirusTotal search without uploading a file.", VirusTotalWebCommand);
        Register("shodanweb", "OSINT+", "shodanweb <query>", "Open a Shodan web search.", ShodanWebCommand);
        Register("censysweb", "OSINT+", "censysweb <query>", "Open a Censys host search.", CensysWebCommand);
    }

    private ShellResult OsintOverviewCommand(string[] args, string raw) =>
        Ok(
            "KURO PASSIVE RESEARCH\n" +
            "────────────────────────────────────────────────────────\n" +
            "Email safety:   breachcheck, pwnedpass, emailosint, gravatar\n" +
            "Social/public:  instagram, sociallinks, githubuser\n" +
            "Domains/web:    crtsh, wayback, whoisweb, urlscanweb\n" +
            "Threat context: virustotalweb, shodanweb, censysweb\n" +
            "Open-source:    toolhub, toolstatus, toolopen, sherlock, maigret\n\n" +
            "Use these only for your own accounts, systems you administer, or research you are authorized to perform.\n" +
            "Kuro does not include credential dumps, private-account access, exploitation, stealth, or bulk account attacks.");

    private ShellResult BreachCheckCommand(string[] args, string raw)
    {
        if (args.Length == 0 || !MailAddress.TryCreate(args[0], out MailAddress? address))
            return Error("breachcheck: enter a valid email address");

        string? key = MegaGetKuroSecret("HIBP_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            return Error(
                "breachcheck: HIBP requires an API key.\n" +
                "Set it for this Kuro profile with:\n" +
                "  set HIBP_API_KEY=your-key\n" +
                "Only check addresses you own or are authorized to investigate.");
        }

        string endpoint =
            "https://haveibeenpwned.com/api/v3/breachedaccount/" +
            Uri.EscapeDataString(address.Address) +
            "?truncateResponse=false";

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, endpoint);
            request.Headers.TryAddWithoutValidation("hibp-api-key", key);
            request.Headers.TryAddWithoutValidation("user-agent", "Kuro-Terminal/" + Version);

            using HttpResponseMessage response =
                MegaOsintHttp.SendAsync(request).GetAwaiter().GetResult();

            if (response.StatusCode == HttpStatusCode.NotFound)
                return Ok("No breaches were returned for this address by HIBP.");

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
                return Error("breachcheck: the HIBP API key was rejected");

            if ((int)response.StatusCode == 429)
                return Error("breachcheck: HIBP rate limit reached; wait and try again");

            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                return Error($"breachcheck: HIBP returned {(int)response.StatusCode} {response.ReasonPhrase}");

            using JsonDocument document = JsonDocument.Parse(body);
            List<string> entries = new();

            foreach (JsonElement breach in document.RootElement.EnumerateArray())
            {
                string name = MegaJsonString(breach, "Name");
                string title = MegaJsonString(breach, "Title");
                string domain = MegaJsonString(breach, "Domain");
                string date = MegaJsonString(breach, "BreachDate");
                bool verified = MegaJsonBool(breach, "IsVerified");
                bool sensitive = MegaJsonBool(breach, "IsSensitive");

                string dataClasses = breach.TryGetProperty("DataClasses", out JsonElement classes) &&
                                     classes.ValueKind == JsonValueKind.Array
                    ? string.Join(", ", classes.EnumerateArray()
                        .Select(item => item.GetString())
                        .Where(item => !string.IsNullOrWhiteSpace(item)))
                    : "not listed";

                entries.Add(
                    $"{title} ({name})\n" +
                    $"  Domain: {domain}\n" +
                    $"  Date: {date}  Verified: {verified}  Sensitive: {sensitive}\n" +
                    $"  Exposed data: {dataClasses}");
            }

            return entries.Count == 0
                ? Ok("HIBP returned an empty breach list.")
                : new ShellResult(
                    $"Found in {entries.Count} breach record(s):\n\n" +
                    string.Join("\n\n", entries),
                    IsError: true);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            JsonException or
            TaskCanceledException)
        {
            return Error($"breachcheck: {ex.Message}");
        }
    }

    private ShellResult PwnedPasswordCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("pwnedpass: enter a password to check");

        string password = string.Join(' ', args);
        string hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
        string prefix = hash[..5];
        string suffix = hash[5..];

        try
        {
            using HttpRequestMessage request =
                new(HttpMethod.Get, "https://api.pwnedpasswords.com/range/" + prefix);
            request.Headers.TryAddWithoutValidation("Add-Padding", "true");
            request.Headers.TryAddWithoutValidation("user-agent", "Kuro-Terminal/" + Version);

            using HttpResponseMessage response =
                MegaOsintHttp.SendAsync(request).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            foreach (string line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Trim().Split(':', 2);
                if (parts.Length == 2 &&
                    parts[0].Equals(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return new ShellResult(
                        $"WARNING: this password hash appeared {parts[1]} time(s) in the Pwned Passwords corpus.\n" +
                        "Do not reuse it. Change it anywhere it is active.",
                        IsError: true);
                }
            }

            return Ok(
                "No match was returned by Pwned Passwords.\n" +
                "Kuro sent only the first five SHA-1 characters, not the password or complete hash.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Error($"pwnedpass: {ex.Message}");
        }
    }

    private ShellResult EmailOsintCommand(string[] args, string raw)
    {
        if (args.Length == 0 || !MailAddress.TryCreate(args[0], out MailAddress? address))
            return Error("emailosint: enter a valid email address");

        string normalized = address.Address.Trim().ToLowerInvariant();
        string domain = address.Host.ToLowerInvariant();
        string md5 = Convert.ToHexString(
                MD5.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant();

        string dns;
        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(domain);
            dns = addresses.Length == 0
                ? "no A/AAAA result"
                : string.Join(", ", addresses.Select(item => item.ToString()).Take(8));
        }
        catch
        {
            dns = "did not resolve";
        }

        return Ok(
            $"Address: {normalized}\n" +
            $"Domain: {domain}\n" +
            $"Domain DNS: {dns}\n" +
            $"Gravatar hash: {md5}\n" +
            $"Gravatar profile: https://gravatar.com/{md5}\n" +
            $"Public avatar: https://www.gravatar.com/avatar/{md5}?d=404\n\n" +
            "Suggested authorized checks:\n" +
            $"  breachcheck {normalized}\n" +
            $"  domainlookup {domain}\n" +
            $"  securityheaders https://{domain}\n" +
            $"  crtsh {domain}\n\n" +
            "This does not verify that the mailbox exists and does not send mail.");
    }

    private ShellResult GravatarCommand(string[] args, string raw)
    {
        if (args.Length == 0 || !MailAddress.TryCreate(args[0], out MailAddress? address))
            return Error("gravatar: enter a valid email address");

        string normalized = address.Address.Trim().ToLowerInvariant();
        string md5 = Convert.ToHexString(
                MD5.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant();

        return Ok(
            $"Profile: https://gravatar.com/{md5}\n" +
            $"Avatar:  https://www.gravatar.com/avatar/{md5}?d=404");
    }

    private ShellResult InstagramCommand(string[] args, string raw)
    {
        if (args.Length == 0 || !Regex.IsMatch(args[0], "^[A-Za-z0-9._]{1,30}$"))
            return Error("instagram: enter a valid public username");

        return MegaOpenResearchUrl(
            "https://www.instagram.com/" + Uri.EscapeDataString(args[0]) + "/",
            "Instagram profile");
    }

    private ShellResult SocialLinksCommand(string[] args, string raw)
    {
        if (args.Length == 0 ||
            !Regex.IsMatch(args[0], "^[A-Za-z0-9._-]{1,64}$"))
            return Error("sociallinks: enter a username");

        string username = args[0];
        string encoded = Uri.EscapeDataString(username);
        Dictionary<string, string> links = new()
        {
            ["GitHub"] = "https://github.com/" + encoded,
            ["Instagram"] = "https://www.instagram.com/" + encoded + "/",
            ["Reddit"] = "https://www.reddit.com/user/" + encoded + "/",
            ["X"] = "https://x.com/" + encoded,
            ["TikTok"] = "https://www.tiktok.com/@" + encoded,
            ["YouTube"] = "https://www.youtube.com/@" + encoded,
            ["Twitch"] = "https://www.twitch.tv/" + encoded
        };

        if (args.Any(arg => arg.Equals("--open", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (string url in links.Values)
                MegaTryOpenUrl(url);
        }

        return Ok(string.Join(
            Environment.NewLine,
            links.Select(item => $"{item.Key,-10} {item.Value}")));
    }

    private ShellResult GitHubUserCommand(string[] args, string raw)
    {
        if (args.Length == 0 ||
            !Regex.IsMatch(args[0], "^[A-Za-z0-9-]{1,39}$"))
            return Error("githubuser: enter a GitHub username");

        try
        {
            string body = MegaOsintHttp.GetStringAsync(
                    "https://api.github.com/users/" +
                    Uri.EscapeDataString(args[0]))
                .GetAwaiter()
                .GetResult();

            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;

            return Ok(
                $"Login: {MegaJsonString(root, "login")}\n" +
                $"Name: {MegaJsonString(root, "name")}\n" +
                $"Company: {MegaJsonString(root, "company")}\n" +
                $"Location: {MegaJsonString(root, "location")}\n" +
                $"Bio: {MegaJsonString(root, "bio")}\n" +
                $"Public repos: {MegaJsonNumber(root, "public_repos")}\n" +
                $"Followers: {MegaJsonNumber(root, "followers")}\n" +
                $"Following: {MegaJsonNumber(root, "following")}\n" +
                $"Created: {MegaJsonString(root, "created_at")}\n" +
                $"Profile: {MegaJsonString(root, "html_url")}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Error("githubuser: user not found");
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            JsonException or
            TaskCanceledException)
        {
            return Error($"githubuser: {ex.Message}");
        }
    }

    private ShellResult CrtShCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("crtsh: enter a domain");

        string domain = MegaNormalizeDomain(args[0]);
        if (!Regex.IsMatch(
                domain,
                "^(?=.{1,253}$)([A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\\.)+[A-Za-z]{2,63}$"))
            return Error("crtsh: enter a valid domain");

        try
        {
            string url =
                "https://crt.sh/?q=%25." +
                Uri.EscapeDataString(domain) +
                "&output=json";

            string body = MegaOsintHttp.GetStringAsync(url)
                .GetAwaiter()
                .GetResult();

            using JsonDocument document = JsonDocument.Parse(body);
            string[] names = document.RootElement
                .EnumerateArray()
                .SelectMany(item =>
                    MegaJsonString(item, "name_value")
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries))
                .Select(name => name.Trim().TrimStart('*').TrimStart('.').ToLowerInvariant())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .Take(250)
                .ToArray();

            return names.Length == 0
                ? Ok("crt.sh returned no certificate names.")
                : Ok(
                    $"Certificate-transparency names for {domain} ({names.Length} shown):\n" +
                    string.Join(Environment.NewLine, names));
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            JsonException or
            TaskCanceledException)
        {
            return Error($"crtsh: {ex.Message}");
        }
    }

    private ShellResult WaybackCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("wayback: enter a URL");

        string url = MegaEnsureAbsoluteUrl(string.Join(' ', args));
        return MegaOpenResearchUrl(
            "https://web.archive.org/web/*/" + Uri.EscapeDataString(url),
            "Wayback history");
    }

    private ShellResult ArchiveUrlCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("archiveurl: enter a URL");

        string url = MegaEnsureAbsoluteUrl(string.Join(' ', args));
        return MegaOpenResearchUrl(
            "https://web.archive.org/save/" + url,
            "Internet Archive save page");
    }

    private ShellResult SearchWebCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("searchweb: enter a query");

        return MegaOpenResearchUrl(
            "https://www.google.com/search?q=" +
            Uri.EscapeDataString(string.Join(' ', args)),
            "web search");
    }

    private ShellResult WhoIsWebCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("whoisweb: enter a domain");

        string domain = MegaNormalizeDomain(args[0]);
        return MegaOpenResearchUrl(
            "https://lookup.icann.org/en/lookup?name=" +
            Uri.EscapeDataString(domain),
            "ICANN lookup");
    }

    private ShellResult UrlScanWebCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("urlscanweb: enter a domain or URL");

        return MegaOpenResearchUrl(
            "https://urlscan.io/search/#" +
            Uri.EscapeDataString(string.Join(' ', args)),
            "urlscan.io search");
    }

    private ShellResult VirusTotalWebCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("virustotalweb: enter a hash, domain, or URL");

        return MegaOpenResearchUrl(
            "https://www.virustotal.com/gui/search/" +
            Uri.EscapeDataString(string.Join(' ', args)),
            "VirusTotal search");
    }

    private ShellResult ShodanWebCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("shodanweb: enter a search query");

        return MegaOpenResearchUrl(
            "https://www.shodan.io/search?query=" +
            Uri.EscapeDataString(string.Join(' ', args)),
            "Shodan search");
    }

    private ShellResult CensysWebCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("censysweb: enter a search query");

        return MegaOpenResearchUrl(
            "https://search.censys.io/search?resource=hosts&q=" +
            Uri.EscapeDataString(string.Join(' ', args)),
            "Censys search");
    }

    private ShellResult MegaOpenResearchUrl(string url, string label)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return Ok($"opened {label}: {url}");
        }
        catch (Exception ex)
        {
            return Error($"{label}: {ex.Message}");
        }
    }

    private static void MegaTryOpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private string? MegaGetKuroSecret(string name)
    {
        if (Config.Variables.TryGetValue(name, out string? value) &&
            !string.IsNullOrWhiteSpace(value))
            return value;

        return Environment.GetEnvironmentVariable(name);
    }

    private static HttpClient CreateMegaOsintClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "user-agent",
            "Kuro-Terminal passive research client");
        return client;
    }

    private static string MegaNormalizeDomain(string value)
    {
        string candidate = value.Trim();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri))
            return uri.Host;

        return candidate.Trim('.').ToLowerInvariant();
    }

    private static string MegaEnsureAbsoluteUrl(string value)
    {
        string candidate = value.Trim();
        return Uri.TryCreate(candidate, UriKind.Absolute, out _)
            ? candidate
            : "https://" + candidate;
    }

    private static string MegaJsonString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind == JsonValueKind.Null)
            return string.Empty;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.ToString();
    }

    private static bool MegaJsonBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.True;

    private static string MegaJsonNumber(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value)
            ? value.ToString()
            : string.Empty;
}
