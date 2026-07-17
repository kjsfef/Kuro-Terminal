using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Kuro;

public sealed partial class ShellEngine
{
    private static readonly Dictionary<char, string> MorseCodes = new()
    {
        ['A'] = ".-", ['B'] = "-...", ['C'] = "-.-.", ['D'] = "-..", ['E'] = ".",
        ['F'] = "..-.", ['G'] = "--.", ['H'] = "....", ['I'] = "..", ['J'] = ".---",
        ['K'] = "-.-", ['L'] = ".-..", ['M'] = "--", ['N'] = "-.", ['O'] = "---",
        ['P'] = ".--.", ['Q'] = "--.-", ['R'] = ".-.", ['S'] = "...", ['T'] = "-",
        ['U'] = "..-", ['V'] = "...-", ['W'] = ".--", ['X'] = "-..-", ['Y'] = "-.--",
        ['Z'] = "--..", ['0'] = "-----", ['1'] = ".----", ['2'] = "..---", ['3'] = "...--",
        ['4'] = "....-", ['5'] = ".....", ['6'] = "-....", ['7'] = "--...", ['8'] = "---..",
        ['9'] = "----.", ['.'] = ".-.-.-", [','] = "--..--", ['?'] = "..--..", ['!'] = "-.-.--"
    };

    private void RegisterDeveloperCommands()
    {
        Register("jsontext", "Developer", "jsontext <json>", "Validate and pretty-print inline JSON.", JsonText);
        Register("jsonget", "Developer", "jsonget <file> <dot.path>", "Read a value from a JSON file using a simple dot path.", JsonGet);
        Register("jsonkeys", "Developer", "jsonkeys <file>", "List top-level JSON object keys.", JsonKeys);
        Register("xml", "Developer", "xml <file>", "Validate and pretty-print an XML file.", FormatXml);
        Register("csvinfo", "Developer", "csvinfo <file>", "Show basic CSV row and column information.", CsvInfo);
        Register("regex", "Developer", "regex <pattern> <text>", "Test a regular expression with a safety timeout.", RegexTest);
        Register("regexreplace", "Developer", "regexreplace <pattern> <replacement> <text>", "Replace regex matches with a safety timeout.", RegexReplaceCommand);
        Register("escape", "Developer", "escape <text>", "Escape control characters for C#-style display.", EscapeText);
        Register("unescape", "Developer", "unescape <text>", "Unescape common backslash sequences.", UnescapeText);
        Register("urlencode", "Developer", "urlencode <text>", "Percent-encode text for a URL component.", UrlEncode);
        Register("urldecode", "Developer", "urldecode <text>", "Decode percent-encoded URL text.", UrlDecode);
        Register("urlparse", "Developer", "urlparse <url>", "Break a URL into its components.", UrlParse);
        Register("htmlencode", "Developer", "htmlencode <text>", "HTML-encode text.", HtmlEncode);
        Register("htmldecode", "Developer", "htmldecode <text>", "HTML-decode text.", HtmlDecode);
        Register("jwt-decode", "Developer", "jwt-decode <token>", "Decode JWT header and payload without verifying the signature.", JwtDecode, "jwt");
        Register("rot13", "Developer", "rot13 <text>", "Apply the reversible ROT13 transformation.", Rot13);
        Register("binary", "Developer", "binary <text>", "Convert UTF-8 text to binary bytes.", ToBinary);
        Register("unbinary", "Developer", "unbinary <bits>", "Convert binary bytes back to UTF-8 text.", FromBinary);
        Register("morse", "Developer", "morse <text>", "Encode supported text as Morse code.", MorseEncode);
        Register("unmorse", "Developer", "unmorse <code>", "Decode Morse code separated by spaces and slashes.", MorseDecode);
        Register("slug", "Developer", "slug <text>", "Create a lowercase URL-friendly slug.", Slugify);
        Register("case", "Developer", "case <camel|pascal|snake|kebab|title> <text>", "Convert text between common naming styles.", ConvertCase);
        Register("bytes", "Developer", "bytes <number>", "Format a byte count into readable units.", FormatBytesCommand);
        Register("timestamp", "Developer", "timestamp [unix-seconds]", "Show or convert a Unix timestamp.", TimestampCommand);
        Register("unix", "Developer", "unix [date/time]", "Convert a local date/time to Unix seconds.", UnixCommand);
        Register("guid", "Developer", "guid", "Generate a new GUID.", (_, _) => Ok(Guid.NewGuid().ToString("D")));
        Register("lorem", "Developer", "lorem [word-count]", "Generate placeholder text.", LoremCommand);
        Register("semver", "Developer", "semver <version1> <version2>", "Compare two dotted version numbers.", SemverCommand);
    }

    private ShellResult JsonText(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("jsontext: missing JSON text");
        try
        {
            using JsonDocument document = JsonDocument.Parse(string.Join(' ', args));
            return Ok(JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (JsonException ex)
        {
            return Error($"jsontext: invalid JSON: {ex.Message}");
        }
    }

    private ShellResult JsonGet(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("jsonget: use jsonget <file> <dot.path>");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"jsonget: file not found: {args[0]}");
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement current = document.RootElement;
            foreach (string part in args[1].Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out JsonElement property))
                {
                    current = property;
                }
                else if (current.ValueKind == JsonValueKind.Array && int.TryParse(part, out int index) &&
                         index >= 0 && index < current.GetArrayLength())
                {
                    current = current[index];
                }
                else
                {
                    return Error($"jsonget: path segment not found: {part}");
                }
            }
            return Ok(JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (JsonException ex)
        {
            return Error($"jsonget: invalid JSON: {ex.Message}");
        }
    }

    private ShellResult JsonKeys(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("jsonkeys: missing file path");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"jsonkeys: file not found: {args[0]}");
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Error("jsonkeys: root value is not an object");
            return Ok(string.Join(Environment.NewLine,
                document.RootElement.EnumerateObject().Select(property => property.Name)));
        }
        catch (JsonException ex)
        {
            return Error($"jsonkeys: invalid JSON: {ex.Message}");
        }
    }

    private ShellResult FormatXml(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("xml: missing file path");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"xml: file not found: {args[0]}");
        try
        {
            XDocument document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            return Ok(document.ToString());
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException)
        {
            return Error($"xml: invalid XML: {ex.Message}");
        }
    }

    private ShellResult CsvInfo(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("csvinfo: missing file path");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"csvinfo: file not found: {args[0]}");

        string[] lines = File.ReadLines(path).Take(100_001).ToArray();
        if (lines.Length == 0)
            return Ok("CSV is empty.");
        List<string> header = ParseCsvLine(lines[0]);
        int maxColumns = lines.Take(5000).Select(line => ParseCsvLine(line).Count).DefaultIfEmpty(0).Max();
        bool truncated = lines.Length > 100_000;
        return Ok($"Rows:        {(lines.Length - 1):n0}{(truncated ? "+" : string.Empty)}\n" +
                  $"Columns:     {maxColumns}\n" +
                  $"Header:      {string.Join(" | ", header)}\n" +
                  $"File size:   {FormatSize(new FileInfo(path).Length)}");
    }

    private ShellResult RegexTest(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("regex: use regex <pattern> <text>");
        try
        {
            Regex expression = new(args[0], RegexOptions.None, TimeSpan.FromMilliseconds(500));
            MatchCollection matches = expression.Matches(string.Join(' ', args.Skip(1)));
            if (matches.Count == 0)
                return Ok("No matches.");
            return Ok(string.Join(Environment.NewLine,
                matches.Cast<Match>().Take(100).Select((match, index) =>
                    $"{index + 1,3}. index={match.Index} length={match.Length} value={match.Value}")));
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            return Error($"regex: {ex.Message}");
        }
    }

    private ShellResult RegexReplaceCommand(string[] args, string raw)
    {
        if (args.Length < 3)
            return Error("regexreplace: use regexreplace <pattern> <replacement> <text>");
        try
        {
            Regex expression = new(args[0], RegexOptions.None, TimeSpan.FromMilliseconds(500));
            return Ok(expression.Replace(string.Join(' ', args.Skip(2)), args[1]));
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            return Error($"regexreplace: {ex.Message}");
        }
    }

    private ShellResult EscapeText(string[] args, string raw)
    {
        string text = string.Join(' ', args);
        return Ok(text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal));
    }

    private ShellResult UnescapeText(string[] args, string raw)
    {
        try { return Ok(Regex.Unescape(string.Join(' ', args))); }
        catch (ArgumentException ex) { return Error($"unescape: {ex.Message}"); }
    }

    private ShellResult UrlEncode(string[] args, string raw) =>
        Ok(Uri.EscapeDataString(string.Join(' ', args)));

    private ShellResult UrlDecode(string[] args, string raw)
    {
        try { return Ok(Uri.UnescapeDataString(string.Join(' ', args))); }
        catch (UriFormatException ex) { return Error($"urldecode: {ex.Message}"); }
    }

    private ShellResult UrlParse(string[] args, string raw)
    {
        if (args.Length == 0 || !Uri.TryCreate(args[0], UriKind.Absolute, out Uri? uri))
            return Error("urlparse: enter a complete absolute URL");
        return Ok($"Scheme:   {uri.Scheme}\nHost:     {uri.Host}\nPort:     {uri.Port}\nPath:     {uri.AbsolutePath}\nQuery:    {uri.Query}\nFragment: {uri.Fragment}\nUserInfo: {uri.UserInfo}\nAuthority:{uri.Authority}");
    }

    private ShellResult HtmlEncode(string[] args, string raw) =>
        Ok(WebUtility.HtmlEncode(string.Join(' ', args)));

    private ShellResult HtmlDecode(string[] args, string raw) =>
        Ok(WebUtility.HtmlDecode(string.Join(' ', args)));

    private ShellResult JwtDecode(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("jwt-decode: missing token");
        string[] parts = args[0].Split('.');
        if (parts.Length < 2)
            return Error("jwt-decode: token must contain at least header and payload segments");
        try
        {
            string header = DecodeBase64Url(parts[0]);
            string payload = DecodeBase64Url(parts[1]);
            string formattedHeader = PrettyJsonOrOriginal(header);
            string formattedPayload = PrettyJsonOrOriginal(payload);
            return Ok("HEADER\n" + formattedHeader + "\n\nPAYLOAD\n" + formattedPayload +
                      "\n\nNote: the signature was not verified.");
        }
        catch (FormatException)
        {
            return Error("jwt-decode: invalid Base64URL data");
        }
    }

    private ShellResult Rot13(string[] args, string raw)
    {
        char[] chars = string.Join(' ', args).ToCharArray();
        for (int index = 0; index < chars.Length; index++)
        {
            char value = chars[index];
            if (value is >= 'a' and <= 'z')
                chars[index] = (char)('a' + (value - 'a' + 13) % 26);
            else if (value is >= 'A' and <= 'Z')
                chars[index] = (char)('A' + (value - 'A' + 13) % 26);
        }
        return Ok(new string(chars));
    }

    private ShellResult ToBinary(string[] args, string raw) =>
        Ok(string.Join(' ', Encoding.UTF8.GetBytes(string.Join(' ', args))
            .Select(value => Convert.ToString(value, 2).PadLeft(8, '0'))));

    private ShellResult FromBinary(string[] args, string raw)
    {
        string joined = string.Join(string.Empty, args).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (joined.Length == 0 || joined.Length % 8 != 0 || joined.Any(value => value is not ('0' or '1')))
            return Error("unbinary: supply binary data in complete 8-bit bytes");
        try
        {
            byte[] bytes = Enumerable.Range(0, joined.Length / 8)
                .Select(index => Convert.ToByte(joined.Substring(index * 8, 8), 2))
                .ToArray();
            return Ok(Encoding.UTF8.GetString(bytes));
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            return Error("unbinary: invalid binary data");
        }
    }

    private ShellResult MorseEncode(string[] args, string raw)
    {
        string text = string.Join(' ', args).ToUpperInvariant();
        List<string> result = new();
        foreach (char character in text)
        {
            if (character == ' ')
                result.Add("/");
            else if (MorseCodes.TryGetValue(character, out string? code))
                result.Add(code);
            else
                result.Add("?");
        }
        return Ok(string.Join(' ', result));
    }

    private ShellResult MorseDecode(string[] args, string raw)
    {
        Dictionary<string, char> reverse = MorseCodes.ToDictionary(item => item.Value, item => item.Key);
        StringBuilder output = new();
        foreach (string token in args)
        {
            if (token == "/")
                output.Append(' ');
            else if (reverse.TryGetValue(token, out char character))
                output.Append(character);
            else
                output.Append('?');
        }
        return Ok(output.ToString());
    }

    private ShellResult Slugify(string[] args, string raw)
    {
        string text = string.Join('-', args).ToLowerInvariant();
        text = Regex.Replace(text, "[^a-z0-9-]+", "-");
        text = Regex.Replace(text, "-+", "-").Trim('-');
        return Ok(text);
    }

    private ShellResult ConvertCase(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("case: use case <camel|pascal|snake|kebab|title> <text>");
        string[] words = Regex.Split(string.Join(' ', args.Skip(1)), "[^A-Za-z0-9]+")
            .Where(word => word.Length > 0)
            .Select(word => word.ToLowerInvariant())
            .ToArray();
        if (words.Length == 0)
            return Ok(string.Empty);

        string Cap(string value) => char.ToUpperInvariant(value[0]) + value[1..];
        string result = args[0].ToLowerInvariant() switch
        {
            "camel" => words[0] + string.Concat(words.Skip(1).Select(Cap)),
            "pascal" => string.Concat(words.Select(Cap)),
            "snake" => string.Join('_', words),
            "kebab" => string.Join('-', words),
            "title" => string.Join(' ', words.Select(Cap)),
            _ => string.Empty
        };
        return string.IsNullOrEmpty(result) ? Error("case: unknown style") : Ok(result);
    }

    private ShellResult FormatBytesCommand(string[] args, string raw)
    {
        if (args.Length == 0 || !long.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            return Error("bytes: enter an integer byte count");
        return Ok(FormatSize(value));
    }

    private ShellResult TimestampCommand(string[] args, string raw)
    {
        if (args.Length == 0)
        {
            DateTimeOffset now = DateTimeOffset.Now;
            return Ok($"Unix: {now.ToUnixTimeSeconds()}\nISO:  {now:O}");
        }
        if (!long.TryParse(args[0], out long seconds))
            return Error("timestamp: enter Unix seconds");
        try
        {
            DateTimeOffset value = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return Ok($"UTC:   {value.UtcDateTime:O}\nLocal: {value.LocalDateTime:O}");
        }
        catch (ArgumentOutOfRangeException)
        {
            return Error("timestamp: value is outside the supported range");
        }
    }

    private ShellResult UnixCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Ok(DateTimeOffset.Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        if (!DateTimeOffset.TryParse(string.Join(' ', args), CultureInfo.CurrentCulture,
                DateTimeStyles.AssumeLocal, out DateTimeOffset value))
            return Error("unix: could not parse the date/time");
        return Ok(value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
    }

    private ShellResult LoremCommand(string[] args, string raw)
    {
        int count = 30;
        if (args.Length > 0 && int.TryParse(args[0], out int parsed))
            count = Math.Clamp(parsed, 1, 500);
        string[] words =
        [
            "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit",
            "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore",
            "magna", "aliqua", "enim", "ad", "minim", "veniam", "quis", "nostrud"
        ];
        return Ok(string.Join(' ', Enumerable.Range(0, count).Select(index => words[index % words.Length])) + ".");
    }

    private ShellResult SemverCommand(string[] args, string raw)
    {
        if (args.Length < 2 || !System.Version.TryParse(args[0], out System.Version? first) || !System.Version.TryParse(args[1], out System.Version? second))
            return Error("semver: use semver <version1> <version2>");
        int comparison = first.CompareTo(second);
        return Ok(comparison == 0 ? "equal" : comparison < 0 ? $"{first} < {second}" : $"{first} > {second}");
    }

    private static List<string> ParseCsvLine(string line)
    {
        List<string> fields = new();
        StringBuilder current = new();
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }

    private static string DecodeBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    private static string PrettyJsonOrOriginal(string value)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return value;
        }
    }
}
