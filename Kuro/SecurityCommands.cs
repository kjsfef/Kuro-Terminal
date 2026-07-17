using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kuro;

public sealed partial class ShellEngine
{
    private static readonly HttpClient WebClient = CreateWebClient();

    private static readonly string[] PassphraseWords =
    [
        "amber", "anchor", "apex", "atlas", "aurora", "bamboo", "beacon", "birch",
        "blue", "breeze", "cedar", "cipher", "cloud", "cobalt", "comet", "coral",
        "crystal", "dawn", "delta", "ember", "falcon", "fern", "fjord", "forest",
        "frost", "galaxy", "glow", "harbor", "horizon", "indigo", "iris", "jade",
        "kuro", "lantern", "lunar", "maple", "meadow", "meteor", "midnight", "mist",
        "moon", "nebula", "night", "nova", "onyx", "orbit", "pine", "pixel",
        "quartz", "raven", "river", "sable", "shadow", "signal", "silver", "solar",
        "sparrow", "storm", "summit", "tide", "timber", "violet", "wave", "willow"
    ];

    private static readonly Dictionary<int, string> CommonPorts = new()
    {
        [20] = "FTP data", [21] = "FTP", [22] = "SSH", [23] = "Telnet", [25] = "SMTP",
        [53] = "DNS", [67] = "DHCP server", [68] = "DHCP client", [80] = "HTTP",
        [110] = "POP3", [123] = "NTP", [143] = "IMAP", [161] = "SNMP", [389] = "LDAP",
        [443] = "HTTPS", [445] = "SMB", [465] = "SMTPS", [587] = "SMTP submission",
        [636] = "LDAPS", [993] = "IMAPS", [995] = "POP3S", [1433] = "Microsoft SQL Server",
        [1521] = "Oracle DB", [2049] = "NFS", [2375] = "Docker API", [3306] = "MySQL",
        [3389] = "RDP", [5432] = "PostgreSQL", [5900] = "VNC", [6379] = "Redis",
        [8080] = "HTTP alternate", [8443] = "HTTPS alternate", [9200] = "Elasticsearch"
    };

    private void RegisterSecurityCommands()
    {
        Register("security", "Security", "security", "Show Kuro's security and privacy command guide.", SecurityGuide, "sec-help");
        Register("ethics", "Security", "ethics", "Show the authorization and safety rules for security tools.", EthicsNotice);
        Register("rdap", "OSINT", "rdap <domain|ip>", "Look up public registration data through RDAP.", RdapLookup, "whois");
        Register("domainlookup", "OSINT", "domainlookup <domain>", "Look up a domain through public RDAP data.", RdapLookup);
        Register("iplookup", "OSINT", "iplookup <ip>", "Look up an IP allocation through public RDAP data.", RdapLookup);
        Register("reverse-dns", "OSINT", "reverse-dns <ip>", "Perform a reverse DNS lookup.", ReverseDns, "ptr");
        Register("headers", "Web Audit", "headers <url>", "Fetch public HTTP response headers.", HttpHeaders);
        Register("status", "Web Audit", "status <url>", "Check HTTP status and response latency.", HttpStatus);
        Register("redirects", "Web Audit", "redirects <url>", "Show a bounded HTTP redirect chain.", RedirectChain);
        Register("robots", "OSINT", "robots <domain|url>", "Fetch a site's public robots.txt file.", RobotsText);
        Register("sitemap", "OSINT", "sitemap <domain|url>", "Fetch a site's public sitemap.xml preview.", SitemapText);
        Register("securityheaders", "Web Audit", "securityheaders <url>", "Review common defensive HTTP security headers.", SecurityHeaders, "secheaders");
        Register("cookiecheck", "Web Audit", "cookiecheck <url>", "Review public Set-Cookie flags for a page.", CookieCheck);
        Register("webcheck", "Web Audit", "webcheck <url>", "Run a compact status and security-header review.", WebCheck);
        Register("tls", "Web Audit", "tls <host> [port]", "Inspect a server TLS certificate without exploitation.", TlsInfo, "certinfo");
        Register("certfile", "Defensive", "certfile <certificate-file>", "Inspect a local X.509 certificate file.", CertificateFileInfo);
        Register("publicip", "Network", "publicip", "Show your public IP using a simple external echo service.", PublicIp);
        Register("portcheck", "Network", "portcheck <host> <port>", "Test one TCP port on an authorized host.", PortCheck);
        Register("cidr", "Network", "cidr <ipv4/prefix>", "Calculate IPv4 CIDR network information.", CidrInfo);
        Register("ipclass", "Network", "ipclass <ip>", "Classify an IP as public, private, loopback, or multicast.", IpClass);
        Register("hostcheck", "Network", "hostcheck <host>", "Resolve a host and summarize returned addresses.", HostCheck);
        Register("portname", "Network", "portname <port>", "Show a common service name for a port.", PortName);
        Register("defang", "Defensive", "defang <indicator>", "Defang URLs, domains, and IP indicators for safe sharing.", Defang);
        Register("refang", "Defensive", "refang <indicator>", "Reverse common indicator defanging.", Refang);
        Register("ioc", "Defensive", "ioc <value>", "Classify a possible URL, IP, domain, email, or hash indicator.", ClassifyIoc);
        Register("hashid", "Defensive", "hashid <hash>", "Identify likely hash algorithms by format and length.", HashIdentify);
        Register("entropy", "Defensive", "entropy <text>", "Calculate Shannon entropy for local text.", EntropyCommand);
        Register("password", "Defensive", "password [length]", "Generate a cryptographically random local password.", GeneratePassword, "passgen");
        Register("passphrase", "Defensive", "passphrase [words]", "Generate a random local passphrase.", GeneratePassphrase);
        Register("passwordcheck", "Defensive", "passwordcheck <password>", "Estimate password strength locally without sending it anywhere.", PasswordCheck);
        Register("emailcheck", "OSINT", "emailcheck <address>", "Validate email syntax and resolve its public domain.", EmailCheck);
        Register("urlclean", "Privacy", "urlclean <url>", "Remove common tracking parameters from a URL.", CleanUrl);
        Register("domainparts", "OSINT", "domainparts <domain|url>", "Break a host name into basic labels.", DomainParts);
        Register("randombytes", "Defensive", "randombytes [count]", "Generate cryptographically random bytes as hexadecimal.", RandomBytes);
        Register("useragent", "Web Audit", "useragent", "Show the HTTP user agent used by Kuro tools.", (_, _) => Ok("Kuro-Terminal/" + Version));
    }

    private ShellResult SecurityGuide(string[] args, string raw) =>
        Ok("KURO SECURITY / PRIVACY TOOLKIT\n" +
           "Research:     rdap, domainlookup, iplookup, reverse-dns, robots, sitemap\n" +
           "Web review:   headers, status, redirects, securityheaders, cookiecheck, webcheck, tls\n" +
           "Defensive:    checksum, filetype, hexdump, strings, ioc, hashid, entropy, defang\n" +
           "Local safety: password, passphrase, passwordcheck, randombytes, urlclean\n" +
           "Open source:  tools, subfinder, amasspassive, metadata, secretscan, trivyfs, sbom, vulnscan\n\n" +
           "Run network and web checks only against public data or systems you are authorized to test.");

    private ShellResult EthicsNotice(string[] args, string raw) =>
        Ok("Kuro's security commands are intended for learning, public-data research, diagnostics, and defense.\n" +
           "Do not use them to invade privacy, bypass access controls, disrupt services, or test systems without explicit authorization.\n" +
           "Kuro intentionally does not include exploit automation, credential attacks, malware, stealth, or broad port scanning.");

    private ShellResult RdapLookup(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("rdap: missing domain or IP");
        string value = NormalizeHostInput(args[0]);
        bool isIp = IPAddress.TryParse(value, out _);
        string endpoint = isIp
            ? "https://rdap.org/ip/" + Uri.EscapeDataString(value)
            : "https://rdap.org/domain/" + Uri.EscapeDataString(value);
        try
        {
            string json = WebClient.GetStringAsync(endpoint).GetAwaiter().GetResult();
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            StringBuilder output = new();
            AppendJsonProperty(output, root, "objectClassName", "Object class");
            AppendJsonProperty(output, root, "handle", "Handle");
            AppendJsonProperty(output, root, "ldhName", "Domain");
            AppendJsonProperty(output, root, "name", "Name");
            AppendJsonProperty(output, root, "startAddress", "Start address");
            AppendJsonProperty(output, root, "endAddress", "End address");
            AppendJsonProperty(output, root, "ipVersion", "IP version");
            AppendJsonProperty(output, root, "country", "Country");
            AppendJsonArray(output, root, "status", "Status");

            if (root.TryGetProperty("events", out JsonElement events) && events.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in events.EnumerateArray().Take(12))
                {
                    string action = GetJsonString(item, "eventAction");
                    string date = GetJsonString(item, "eventDate");
                    if (!string.IsNullOrEmpty(action) || !string.IsNullOrEmpty(date))
                        output.AppendLine($"{action,-18} {date}");
                }
            }

            if (output.Length == 0)
                output.AppendLine(JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }));
            output.AppendLine("Source: public RDAP data");
            return Ok(output.ToString().TrimEnd());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Error($"rdap: lookup failed: {ex.Message}");
        }
    }

    private ShellResult ReverseDns(string[] args, string raw)
    {
        if (args.Length == 0 || !IPAddress.TryParse(args[0], out IPAddress? address))
            return Error("reverse-dns: enter a valid IP address");
        try
        {
            IPHostEntry result = Dns.GetHostEntry(address);
            return Ok($"Host:    {result.HostName}\nAliases: {string.Join(", ", result.Aliases)}\nAddresses:\n{string.Join(Environment.NewLine, result.AddressList.Select(item => "  " + item))}");
        }
        catch (SocketException ex)
        {
            return Error($"reverse-dns: {ex.Message}");
        }
    }

    private ShellResult HttpHeaders(string[] args, string raw)
    {
        if (!TryNormalizeHttpUrl(args, out Uri? uri, out ShellResult error))
            return error;
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            using HttpResponseMessage response = WebClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
            StringBuilder output = new();
            output.AppendLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            foreach (var header in response.Headers.OrderBy(item => item.Key))
                output.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            foreach (var header in response.Content.Headers.OrderBy(item => item.Key))
                output.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            return Ok(output.ToString().TrimEnd());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Error($"headers: request failed: {ex.Message}");
        }
    }

    private ShellResult HttpStatus(string[] args, string raw)
    {
        if (!TryNormalizeHttpUrl(args, out Uri? uri, out ShellResult error))
            return error;
        try
        {
            Stopwatch timer = Stopwatch.StartNew();
            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            using HttpResponseMessage response = WebClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
            timer.Stop();
            return new ShellResult($"URL:      {response.RequestMessage?.RequestUri}\nStatus:   {(int)response.StatusCode} {response.ReasonPhrase}\nLatency:  {timer.ElapsedMilliseconds} ms\nProtocol: {response.Version}", !response.IsSuccessStatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Error($"status: request failed: {ex.Message}");
        }
    }

    private ShellResult RedirectChain(string[] args, string raw)
    {
        if (!TryNormalizeHttpUrl(args, out Uri? uri, out ShellResult error))
            return error;
        try
        {
            using HttpClientHandler handler = new() { AllowAutoRedirect = false };
            using HttpClient client = new(handler) { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Kuro-Terminal/" + Version);
            StringBuilder output = new();
            Uri current = uri;
            for (int hop = 0; hop < 10; hop++)
            {
                using HttpResponseMessage response = client.GetAsync(current, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
                output.AppendLine($"{hop,2}: {(int)response.StatusCode} {current}");
                if ((int)response.StatusCode is < 300 or >= 400 || response.Headers.Location is null)
                    break;
                current = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(current, response.Headers.Location);
            }
            return Ok(output.ToString().TrimEnd());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Error($"redirects: request failed: {ex.Message}");
        }
    }

    private ShellResult RobotsText(string[] args, string raw) => FetchPublicText(args, "/robots.txt", "robots");

    private ShellResult SitemapText(string[] args, string raw) => FetchPublicText(args, "/sitemap.xml", "sitemap");

    private ShellResult SecurityHeaders(string[] args, string raw)
    {
        if (!TryNormalizeHttpUrl(args, out Uri? uri, out ShellResult error))
            return error;
        try
        {
            using HttpResponseMessage response = WebClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            Dictionary<string, string> checks = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Strict-Transport-Security"] = "HSTS",
                ["Content-Security-Policy"] = "CSP",
                ["X-Content-Type-Options"] = "MIME sniffing protection",
                ["Referrer-Policy"] = "Referrer policy",
                ["Permissions-Policy"] = "Browser feature policy",
                ["Cross-Origin-Opener-Policy"] = "Cross-origin opener isolation",
                ["Cross-Origin-Resource-Policy"] = "Cross-origin resource policy",
                ["X-Frame-Options"] = "Legacy frame protection"
            };
            StringBuilder output = new();
            output.AppendLine($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            int present = 0;
            foreach ((string header, string description) in checks)
            {
                bool found = response.Headers.Contains(header) || response.Content.Headers.Contains(header);
                if (found) present++;
                output.AppendLine($"{(found ? "[+]" : "[-]")} {header,-34} {description}");
            }
            output.AppendLine($"Present: {present}/{checks.Count}");
            output.AppendLine("This is a basic configuration review, not a vulnerability verdict.");
            return Ok(output.ToString().TrimEnd());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Error($"securityheaders: request failed: {ex.Message}");
        }
    }

    private ShellResult CookieCheck(string[] args, string raw)
    {
        if (!TryNormalizeHttpUrl(args, out Uri? uri, out ShellResult error))
            return error;
        try
        {
            using HttpResponseMessage response = WebClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if (!response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies))
                return Ok("No Set-Cookie headers were returned.");
            StringBuilder output = new();
            int index = 0;
            foreach (string cookie in cookies.Take(50))
            {
                index++;
                string name = cookie.Split('=', 2)[0];
                bool secure = cookie.Contains("; Secure", StringComparison.OrdinalIgnoreCase);
                bool httpOnly = cookie.Contains("; HttpOnly", StringComparison.OrdinalIgnoreCase);
                bool sameSite = cookie.Contains("SameSite=", StringComparison.OrdinalIgnoreCase);
                output.AppendLine($"{index,2}. {name}  Secure={secure}  HttpOnly={httpOnly}  SameSite={sameSite}");
            }
            output.AppendLine("This checks flags only and does not determine whether a cookie is sensitive.");
            return Ok(output.ToString().TrimEnd());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Error($"cookiecheck: request failed: {ex.Message}");
        }
    }

    private ShellResult WebCheck(string[] args, string raw)
    {
        ShellResult status = HttpStatus(args, raw);
        if (status.IsError)
            return status;
        ShellResult headers = SecurityHeaders(args, raw);
        return new ShellResult(status.Output + "\n\n" + headers.Output, headers.IsError);
    }

    private ShellResult TlsInfo(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("tls: missing host");
        string host = NormalizeHostInput(args[0]);
        int port = 443;
        if (args.Length > 1 && (!int.TryParse(args[1], out port) || port is < 1 or > 65535))
            return Error("tls: port must be from 1 to 65535");
        try
        {
            SslPolicyErrors policyErrors = SslPolicyErrors.None;
            using TcpClient client = new();
            client.ConnectAsync(host, port).WaitAsync(TimeSpan.FromSeconds(6)).GetAwaiter().GetResult();
            using SslStream stream = new(client.GetStream(), false, (_, _, _, errors) =>
            {
                policyErrors = errors;
                return true;
            });
            stream.AuthenticateAsClientAsync(host).WaitAsync(TimeSpan.FromSeconds(6)).GetAwaiter().GetResult();
            if (stream.RemoteCertificate is null)
                return Error("tls: server did not provide a certificate");
            using X509Certificate2 certificate = new(stream.RemoteCertificate);
            return Ok(FormatCertificate(certificate) +
                      $"\nTLS protocol: {stream.SslProtocol}\nCipher:       {stream.NegotiatedCipherSuite}\nPolicy:       {policyErrors}");
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException or TimeoutException)
        {
            return Error($"tls: connection failed: {ex.Message}");
        }
    }

    private ShellResult CertificateFileInfo(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("certfile: missing certificate path");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"certfile: file not found: {args[0]}");
        try
        {
            using X509Certificate2 certificate = new X509Certificate2(path);
            return Ok(FormatCertificate(certificate));
        }
        catch (CryptographicException ex)
        {
            return Error($"certfile: could not load certificate: {ex.Message}");
        }
    }

    private ShellResult PublicIp(string[] args, string raw)
    {
        try
        {
            string value = WebClient.GetStringAsync("https://api.ipify.org").GetAwaiter().GetResult().Trim();
            return Ok(value);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Error($"publicip: lookup failed: {ex.Message}");
        }
    }

    private ShellResult PortCheck(string[] args, string raw)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int port) || port is < 1 or > 65535)
            return Error("portcheck: use portcheck <authorized-host> <port>");
        string host = NormalizeHostInput(args[0]);
        Stopwatch timer = Stopwatch.StartNew();
        try
        {
            using TcpClient client = new();
            client.ConnectAsync(host, port).WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            timer.Stop();
            return Ok($"OPEN  {host}:{port}  {timer.ElapsedMilliseconds} ms\nOnly one TCP port was checked.");
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException)
        {
            timer.Stop();
            return new ShellResult($"CLOSED/FILTERED  {host}:{port}  {timer.ElapsedMilliseconds} ms\n{ex.Message}", true);
        }
    }

    private ShellResult CidrInfo(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("cidr: use cidr <ipv4/prefix>");
        string[] parts = args[0].Split('/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out IPAddress? address) ||
            address.AddressFamily != AddressFamily.InterNetwork ||
            !int.TryParse(parts[1], out int prefix) || prefix is < 0 or > 32)
            return Error("cidr: enter a valid IPv4 CIDR such as 192.168.1.10/24");

        byte[] bytes = address.GetAddressBytes();
        uint ip = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        uint mask = prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
        uint network = ip & mask;
        uint broadcast = network | ~mask;
        ulong total = 1UL << (32 - prefix);
        ulong usable = prefix >= 31 ? total : total - 2;
        return Ok($"Address:    {address}\nPrefix:     /{prefix}\nMask:       {UIntToIp(mask)}\nNetwork:    {UIntToIp(network)}\nBroadcast:  {UIntToIp(broadcast)}\nTotal IPs:  {total:n0}\nUsable:     {usable:n0}");
    }

    private ShellResult IpClass(string[] args, string raw)
    {
        if (args.Length == 0 || !IPAddress.TryParse(args[0], out IPAddress? address))
            return Error("ipclass: enter a valid IP address");
        string classification = ClassifyAddress(address);
        return Ok($"Address: {address}\nFamily:  {address.AddressFamily}\nClass:   {classification}");
    }

    private ShellResult HostCheck(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("hostcheck: missing host");
        string host = NormalizeHostInput(args[0]);
        try
        {
            IPHostEntry entry = Dns.GetHostEntry(host);
            StringBuilder output = new();
            output.AppendLine("Canonical: " + entry.HostName);
            foreach (IPAddress address in entry.AddressList)
                output.AppendLine($"{address,-40} {ClassifyAddress(address)}");
            return Ok(output.ToString().TrimEnd());
        }
        catch (SocketException ex)
        {
            return Error($"hostcheck: {ex.Message}");
        }
    }

    private ShellResult PortName(string[] args, string raw)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int port) || port is < 1 or > 65535)
            return Error("portname: enter a port from 1 to 65535");
        return Ok(CommonPorts.TryGetValue(port, out string? name) ? name : "No common service name in Kuro's local table.");
    }

    private ShellResult Defang(string[] args, string raw)
    {
        string value = string.Join(' ', args);
        string result = value.Replace("https://", "hxxps://", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "hxxp://", StringComparison.OrdinalIgnoreCase)
            .Replace(".", "[.]", StringComparison.Ordinal);
        return Ok(result);
    }

    private ShellResult Refang(string[] args, string raw)
    {
        string value = string.Join(' ', args);
        string result = value.Replace("hxxps://", "https://", StringComparison.OrdinalIgnoreCase)
            .Replace("hxxp://", "http://", StringComparison.OrdinalIgnoreCase)
            .Replace("[.]", ".", StringComparison.Ordinal)
            .Replace("(.)", ".", StringComparison.Ordinal);
        return Ok(result);
    }

    private ShellResult ClassifyIoc(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("ioc: missing value");
        string value = string.Join(' ', args).Trim();
        string refanged = Refang([value], value).Output;
        string type;
        if (Uri.TryCreate(refanged, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https")
            type = "URL";
        else if (IPAddress.TryParse(refanged, out _))
            type = "IP address";
        else if (Regex.IsMatch(refanged, "^[A-Fa-f0-9]{32}$"))
            type = "MD5-like hash";
        else if (Regex.IsMatch(refanged, "^[A-Fa-f0-9]{40}$"))
            type = "SHA-1-like hash";
        else if (Regex.IsMatch(refanged, "^[A-Fa-f0-9]{64}$"))
            type = "SHA-256-like hash";
        else if (MailAddress.TryCreate(refanged, out _))
            type = "email address";
        else if (Regex.IsMatch(refanged, "^(?=.{1,253}$)([A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\\.)+[A-Za-z]{2,63}$"))
            type = "domain name";
        else
            type = "unknown text";
        return Ok($"Type:     {type}\nOriginal: {value}\nRefanged: {refanged}\nDefanged: {Defang([refanged], refanged).Output}");
    }

    private ShellResult HashIdentify(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("hashid: missing hash");
        string value = args[0].Trim();
        if (!Regex.IsMatch(value, "^[A-Fa-f0-9]+$"))
            return Ok("Not a plain hexadecimal hash.");
        string[] possibilities = value.Length switch
        {
            32 => ["MD5", "NTLM", "MD4"],
            40 => ["SHA-1", "RIPEMD-160"],
            56 => ["SHA-224"],
            64 => ["SHA-256", "BLAKE2s"],
            96 => ["SHA-384"],
            128 => ["SHA-512", "BLAKE2b"],
            _ => ["Unknown hexadecimal digest length"]
        };
        return Ok($"Length: {value.Length} hex characters ({value.Length * 4} bits)\nPossible: {string.Join(", ", possibilities)}\nFormat alone cannot prove the algorithm.");
    }

    private ShellResult EntropyCommand(string[] args, string raw)
    {
        string value = string.Join(' ', args);
        if (value.Length == 0)
            return Error("entropy: missing text");
        double entropy = value.GroupBy(character => character)
            .Select(group => (double)group.Count() / value.Length)
            .Sum(probability => -probability * Math.Log2(probability));
        return Ok($"Length:        {value.Length}\nUnique chars:  {value.Distinct().Count()}\nEntropy:       {entropy:0.000} bits/character\nEstimated:     {entropy * value.Length:0.0} total bits");
    }

    private ShellResult GeneratePassword(string[] args, string raw)
    {
        int length = 24;
        if (args.Length > 0 && int.TryParse(args[0], out int parsed))
            length = Math.Clamp(parsed, 8, 128);
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*()-_=+";
        return Ok(RandomString(alphabet, length));
    }

    private ShellResult GeneratePassphrase(string[] args, string raw)
    {
        int count = 5;
        if (args.Length > 0 && int.TryParse(args[0], out int parsed))
            count = Math.Clamp(parsed, 3, 12);
        return Ok(string.Join('-', Enumerable.Range(0, count)
            .Select(_ => PassphraseWords[RandomNumberGenerator.GetInt32(PassphraseWords.Length)])));
    }

    private ShellResult PasswordCheck(string[] args, string raw)
    {
        string value = string.Join(' ', args);
        if (value.Length == 0)
            return Error("passwordcheck: missing password");
        int score = 0;
        List<string> notes = new();
        if (value.Length >= 12) score += 2; else notes.Add("use at least 12 characters");
        if (value.Length >= 16) score++;
        if (value.Any(char.IsLower)) score++; else notes.Add("add lowercase letters");
        if (value.Any(char.IsUpper)) score++; else notes.Add("add uppercase letters");
        if (value.Any(char.IsDigit)) score++; else notes.Add("add numbers");
        if (value.Any(character => !char.IsLetterOrDigit(character))) score++; else notes.Add("add symbols");
        if (Regex.IsMatch(value, "(.)\\1{2,}")) { score--; notes.Add("avoid repeated characters"); }
        if (Regex.IsMatch(value, "password|qwerty|letmein|admin|1234", RegexOptions.IgnoreCase)) { score -= 3; notes.Add("contains a common pattern"); }
        score = Math.Clamp(score, 0, 7);
        string rating = score switch { <= 2 => "weak", <= 4 => "fair", <= 6 => "strong", _ => "very strong" };
        return Ok($"Rating: {rating} ({score}/7)\nLength: {value.Length}\nAdvice: {(notes.Count == 0 ? "good local structure" : string.Join("; ", notes))}\nNothing was sent over the network.");
    }

    private ShellResult EmailCheck(string[] args, string raw)
    {
        if (args.Length == 0 || !MailAddress.TryCreate(args[0], out MailAddress? address))
            return Error("emailcheck: enter a syntactically valid email address");
        try
        {
            IPAddress[] results = Dns.GetHostAddresses(address.Host);
            return Ok($"Address:      {address.Address}\nDisplay name: {address.DisplayName}\nDomain:       {address.Host}\nDNS:          {(results.Length > 0 ? "resolves" : "no addresses")}\nNote: this does not verify that the mailbox exists.");
        }
        catch (SocketException)
        {
            return new ShellResult($"Address syntax is valid, but the domain did not resolve: {address.Host}", true);
        }
    }

    private ShellResult CleanUrl(string[] args, string raw)
    {
        if (args.Length == 0 || !Uri.TryCreate(args[0], UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
            return Error("urlclean: enter a complete HTTP or HTTPS URL");
        HashSet<string> trackers = new(StringComparer.OrdinalIgnoreCase)
        {
            "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content", "utm_id",
            "gclid", "fbclid", "msclkid", "mc_cid", "mc_eid", "igshid", "ref", "ref_src"
        };
        List<string> kept = new();
        foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string key = Uri.UnescapeDataString(pair.Split('=', 2)[0]);
            if (!trackers.Contains(key))
                kept.Add(pair);
        }
        UriBuilder builder = new(uri) { Query = string.Join('&', kept) };
        return Ok(builder.Uri.ToString());
    }

    private ShellResult DomainParts(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("domainparts: missing domain or URL");
        string host = NormalizeHostInput(args[0]).Trim('.');
        string[] labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length == 0)
            return Error("domainparts: invalid host");
        string suffix = labels.Length >= 1 ? labels[^1] : string.Empty;
        string registeredGuess = labels.Length >= 2 ? labels[^2] + "." + labels[^1] : host;
        string subdomain = labels.Length > 2 ? string.Join('.', labels[..^2]) : "(none)";
        return Ok($"Host:             {host}\nLabels:           {labels.Length}\nSuffix guess:     {suffix}\nRegistered guess: {registeredGuess}\nSubdomain guess:  {subdomain}\nNote: this is a simple label split, not a public-suffix-list decision.");
    }

    private ShellResult RandomBytes(string[] args, string raw)
    {
        int count = 32;
        if (args.Length > 0 && int.TryParse(args[0], out int parsed))
            count = Math.Clamp(parsed, 1, 1024);
        return Ok(Convert.ToHexString(RandomNumberGenerator.GetBytes(count)).ToLowerInvariant());
    }

    private ShellResult FetchPublicText(string[] args, string suffix, string commandName)
    {
        if (args.Length == 0)
            return Error($"{commandName}: missing domain or URL");
        string input = args[0];
        if (!Uri.TryCreate(input, UriKind.Absolute, out Uri? baseUri))
        {
            if (!Uri.TryCreate("https://" + input, UriKind.Absolute, out baseUri))
                return Error($"{commandName}: invalid domain or URL");
        }
        if (baseUri.Scheme is not ("http" or "https"))
            return Error($"{commandName}: only HTTP and HTTPS are supported");
        Uri target = new(baseUri.GetLeftPart(UriPartial.Authority) + suffix);
        try
        {
            using HttpResponseMessage response = WebClient.GetAsync(target, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            string content = ReadLimitedContent(response, 24_000);
            return new ShellResult($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\nURL: {target}\n\n{content}", !response.IsSuccessStatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return Error($"{commandName}: request failed: {ex.Message}");
        }
    }

    private static HttpClient CreateWebClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Kuro-Terminal/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, text/html, */*");
        return client;
    }

    private static bool TryNormalizeHttpUrl(string[] args, out Uri? uri, out ShellResult error)
    {
        uri = null;
        error = ShellResult.Empty;
        if (args.Length == 0)
        {
            error = new ShellResult("missing URL", true);
            return false;
        }
        string input = args[0];
        if (!Uri.TryCreate(input, UriKind.Absolute, out uri))
            Uri.TryCreate("https://" + input, UriKind.Absolute, out uri);
        if (uri is null || uri.Scheme is not ("http" or "https"))
        {
            error = new ShellResult("enter a valid HTTP or HTTPS URL", true);
            return false;
        }
        return true;
    }

    private static string NormalizeHostInput(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            return uri.Host;
        return value.Trim().TrimEnd('.');
    }

    private static void AppendJsonProperty(StringBuilder output, JsonElement root, string property, string label)
    {
        string value = GetJsonString(root, property);
        if (!string.IsNullOrEmpty(value))
            output.AppendLine($"{label,-18} {value}");
    }

    private static void AppendJsonArray(StringBuilder output, JsonElement root, string property, string label)
    {
        if (root.TryGetProperty(property, out JsonElement element) && element.ValueKind == JsonValueKind.Array)
            output.AppendLine($"{label,-18} {string.Join(", ", element.EnumerateArray().Select(item => item.ToString()))}");
    }

    private static string GetJsonString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out JsonElement value))
            return string.Empty;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }

    private static string ReadLimitedContent(HttpResponseMessage response, int maxCharacters)
    {
        using Stream stream = response.Content.ReadAsStream();
        using StreamReader reader = new(stream, Encoding.UTF8, true, 4096, false);
        char[] buffer = new char[maxCharacters];
        int read = reader.ReadBlock(buffer, 0, buffer.Length);
        string value = new(buffer, 0, read);
        if (!reader.EndOfStream)
            value += "\n... response preview truncated";
        return value;
    }

    private static string FormatCertificate(X509Certificate2 certificate)
    {
        StringBuilder output = new();
        output.AppendLine($"Subject:      {certificate.Subject}");
        output.AppendLine($"Issuer:       {certificate.Issuer}");
        output.AppendLine($"Serial:       {certificate.SerialNumber}");
        output.AppendLine($"Valid from:   {certificate.NotBefore:O}");
        output.AppendLine($"Valid until:  {certificate.NotAfter:O}");
        output.AppendLine($"Expired:      {DateTime.Now < certificate.NotBefore || DateTime.Now > certificate.NotAfter}");
        output.AppendLine($"Thumbprint:   {certificate.Thumbprint}");
        output.AppendLine($"Algorithm:    {certificate.SignatureAlgorithm.FriendlyName}");
        output.AppendLine($"Public key:   {certificate.PublicKey.Oid.FriendlyName} ({certificate.PublicKey.Key.KeySize} bits)");
        return output.ToString().TrimEnd();
    }

    private static string UIntToIp(uint value) =>
        $"{(value >> 24) & 255}.{(value >> 16) & 255}.{(value >> 8) & 255}.{value & 255}";

    private static string ClassifyAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return "loopback";
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = address.GetAddressBytes();
            if (bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 169 && bytes[1] == 254))
                return "private/link-local";
            if (bytes[0] is >= 224 and <= 239)
                return "multicast";
            return "public/unicast";
        }
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            return "private/link-local";
        if (address.IsIPv6Multicast)
            return "multicast";
        return "public/unicast";
    }

    private static string RandomString(string alphabet, int length)
    {
        char[] result = new char[length];
        for (int index = 0; index < result.Length; index++)
            result[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(result);
    }
}
