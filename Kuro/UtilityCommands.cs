using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kuro;

public sealed partial class ShellEngine
{
    private void RegisterUtilityCommands()
    {
        Register("echo", "Utilities", "echo <text>", "Print text after variable expansion.", Echo);
        Register("calc", "Utilities", "calc <expression>", "Evaluate arithmetic with + - * / % ^ and parentheses.", Calculate);
        Register("random", "Utilities", "random [min] [max]", "Generate a random integer.", RandomNumber, "rand");
        Register("uuid", "Utilities", "uuid", "Generate a new UUID.", (_, _) => Ok(Guid.NewGuid().ToString()));
        Register("hash", "Utilities", "hash <sha256|sha1|md5> <text|--file path>", "Hash text or a file.", Hash);
        Register("base64", "Utilities", "base64 <encode|decode> <text>", "Encode or decode Base64 text.", Base64);
        Register("upper", "Utilities", "upper <text>", "Convert text to uppercase.", (args, _) => Ok(string.Join(' ', args).ToUpperInvariant()));
        Register("lower", "Utilities", "lower <text>", "Convert text to lowercase.", (args, _) => Ok(string.Join(' ', args).ToLowerInvariant()));
        Register("reverse", "Utilities", "reverse <text>", "Reverse text.", Reverse);
        Register("repeat", "Utilities", "repeat <count> <text>", "Repeat text up to 1,000 times.", Repeat);
        Register("length", "Utilities", "length <text>", "Count characters in text.", (args, _) => Ok(string.Join(' ', args).Length.ToString(CultureInfo.InvariantCulture)), "len");
        Register("json", "Utilities", "json <file>", "Pretty-print and validate a JSON file.", FormatJson);
        Register("hex", "Utilities", "hex <text>", "Convert UTF-8 text to hexadecimal.", HexEncode);
        Register("unhex", "Utilities", "unhex <hex>", "Convert hexadecimal to UTF-8 text.", HexDecode);
    }

    private ShellResult Echo(string[] args, string raw) => Ok(string.Join(' ', args));

    private ShellResult Calculate(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("calc: missing expression");

        string expression = string.Join(' ', args);
        try
        {
            double value = new ExpressionParser(expression).Parse();
            return Ok(value.ToString("G15", CultureInfo.InvariantCulture));
        }
        catch (FormatException ex)
        {
            return Error($"calc: {ex.Message}");
        }
    }

    private ShellResult RandomNumber(string[] args, string raw)
    {
        int min = 0;
        int max = 100;

        if (args.Length == 1)
        {
            if (!int.TryParse(args[0], out max))
                return Error("random: values must be integers");
            min = 0;
        }
        else if (args.Length >= 2)
        {
            if (!int.TryParse(args[0], out min) || !int.TryParse(args[1], out max))
                return Error("random: values must be integers");
        }

        if (min > max)
            (min, max) = (max, min);
        if (min == max)
            return Ok(min.ToString(CultureInfo.InvariantCulture));

        return Ok(Random.Shared.Next(min, max + 1).ToString(CultureInfo.InvariantCulture));
    }

    private ShellResult Hash(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("hash: use hash <sha256|sha1|md5> <text|--file path>");

        HashAlgorithm algorithm = args[0].ToLowerInvariant() switch
        {
            "sha256" => SHA256.Create(),
            "sha1" => SHA1.Create(),
            "md5" => MD5.Create(),
            _ => throw new FormatException("hash: supported algorithms are sha256, sha1, and md5")
        };

        using (algorithm)
        {
            byte[] data;
            if (args[1].Equals("--file", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                    return Error("hash: missing file path");
                string path = ResolvePath(args[2]);
                if (!File.Exists(path))
                    return Error($"hash: file not found: {args[2]}");
                data = File.ReadAllBytes(path);
            }
            else
            {
                data = Encoding.UTF8.GetBytes(string.Join(' ', args.Skip(1)));
            }

            return Ok(Convert.ToHexString(algorithm.ComputeHash(data)).ToLowerInvariant());
        }
    }

    private ShellResult Base64(string[] args, string raw)
    {
        if (args.Length < 2)
            return Error("base64: use base64 encode <text> or base64 decode <text>");

        string action = args[0].ToLowerInvariant();
        string value = string.Join(' ', args.Skip(1));
        try
        {
            return action switch
            {
                "encode" => Ok(Convert.ToBase64String(Encoding.UTF8.GetBytes(value))),
                "decode" => Ok(Encoding.UTF8.GetString(Convert.FromBase64String(value))),
                _ => Error("base64: action must be encode or decode")
            };
        }
        catch (FormatException)
        {
            return Error("base64: invalid Base64 input");
        }
    }

    private ShellResult Reverse(string[] args, string raw)
    {
        char[] characters = string.Join(' ', args).ToCharArray();
        Array.Reverse(characters);
        return Ok(new string(characters));
    }

    private ShellResult Repeat(string[] args, string raw)
    {
        if (args.Length < 2 || !int.TryParse(args[0], out int count))
            return Error("repeat: use repeat <count> <text>");

        count = Math.Clamp(count, 0, 1_000);
        string text = string.Join(' ', args.Skip(1));
        return Ok(string.Join(Environment.NewLine, Enumerable.Repeat(text, count)));
    }

    private ShellResult FormatJson(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("json: missing file path");
        string path = ResolvePath(args[0]);
        if (!File.Exists(path))
            return Error($"json: file not found: {args[0]}");

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return Ok(JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (JsonException ex)
        {
            return Error($"json: invalid JSON at line {ex.LineNumber}, byte {ex.BytePositionInLine}: {ex.Message}");
        }
    }

    private ShellResult HexEncode(string[] args, string raw) =>
        Ok(Convert.ToHexString(Encoding.UTF8.GetBytes(string.Join(' ', args))).ToLowerInvariant());

    private ShellResult HexDecode(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("unhex: missing hexadecimal text");
        try
        {
            return Ok(Encoding.UTF8.GetString(Convert.FromHexString(string.Join(string.Empty, args))));
        }
        catch (FormatException)
        {
            return Error("unhex: invalid hexadecimal input");
        }
    }

    private sealed class ExpressionParser
    {
        private readonly string _text;
        private int _position;

        public ExpressionParser(string text) => _text = text;

        public double Parse()
        {
            double value = ParseExpression();
            SkipWhitespace();
            if (_position != _text.Length)
                throw new FormatException($"unexpected character '{_text[_position]}' at position {_position + 1}");
            return value;
        }

        private double ParseExpression()
        {
            double value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Match('+'))
                    value += ParseTerm();
                else if (Match('-'))
                    value -= ParseTerm();
                else
                    return value;
            }
        }

        private double ParseTerm()
        {
            double value = ParsePower();
            while (true)
            {
                SkipWhitespace();
                if (Match('*'))
                    value *= ParsePower();
                else if (Match('/'))
                {
                    double divisor = ParsePower();
                    if (divisor == 0)
                        throw new FormatException("division by zero");
                    value /= divisor;
                }
                else if (Match('%'))
                {
                    double divisor = ParsePower();
                    if (divisor == 0)
                        throw new FormatException("division by zero");
                    value %= divisor;
                }
                else
                    return value;
            }
        }

        private double ParsePower()
        {
            double value = ParseUnary();
            SkipWhitespace();
            if (Match('^'))
                value = Math.Pow(value, ParsePower());
            return value;
        }

        private double ParseUnary()
        {
            SkipWhitespace();
            if (Match('+'))
                return ParseUnary();
            if (Match('-'))
                return -ParseUnary();
            return ParsePrimary();
        }

        private double ParsePrimary()
        {
            SkipWhitespace();
            if (Match('('))
            {
                double value = ParseExpression();
                SkipWhitespace();
                if (!Match(')'))
                    throw new FormatException("missing closing parenthesis");
                return value;
            }

            int start = _position;
            while (_position < _text.Length &&
                   (char.IsDigit(_text[_position]) || _text[_position] == '.'))
                _position++;

            if (start == _position || !double.TryParse(
                    _text[start.._position], NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                throw new FormatException($"expected a number at position {_position + 1}");

            return number;
        }

        private bool Match(char value)
        {
            if (_position >= _text.Length || _text[_position] != value)
                return false;
            _position++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                _position++;
        }
    }
}
