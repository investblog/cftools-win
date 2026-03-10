using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CFTools.Services;

public record ParseResult(List<string> Domains, List<string> Duplicates, List<string> Invalid);

/// <summary>
/// Extracts and validates domain names from arbitrary text input.
/// Handles URLs, HTML, JSON, CSV, email addresses, IDN/punycode.
/// Best-effort extraction - final validation is done by Cloudflare API.
/// </summary>
public static partial class DomainParser
{
    private static readonly HashSet<string> SpecialSLDs = new(StringComparer.OrdinalIgnoreCase)
    {
        "co",
        "com",
        "net",
        "org",
        "edu",
        "gov",
        "ac",
        "me",
    };

    private static readonly IdnMapping Idn = new();
    private static readonly UTF8Encoding Utf8Strict = new(false, true);
    private static readonly Encoding Latin1 = Encoding.Latin1;
    private static readonly Encoding Windows1251;
    private static readonly char[] TokenDelimiters =
    {
        ' ',
        '\t',
        '\n',
        '\r',
        ',',
        ';',
        '|',
        '<',
        '>',
        '"',
        '\'',
        '(',
        ')',
        '[',
        ']',
        '{',
        '}',
    };

    static DomainParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1251 = Encoding.GetEncoding(1251);
    }

    [GeneratedRegex(
        @"\b((?=[a-z0-9-]{1,63}\.)((?:xn--)?[a-z0-9]+(?:-[a-z0-9]+)*\.)+(?:xn--)?[a-z0-9-]{2,63})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    )]
    private static partial Regex AsciiDomainRegex();

    public static ParseResult Parse(string text, bool rootOnly = true)
    {
        var matches = ExtractPotentialDomains(text);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var domains = new List<string>();
        var duplicates = new List<string>();
        var invalid = new List<string>();

        foreach (var match in matches)
        {
            var domain = NormalizeDomain(match);
            if (string.IsNullOrEmpty(domain))
                continue;

            if (!HasValidTld(domain))
            {
                if (!invalid.Contains(domain, StringComparer.OrdinalIgnoreCase))
                    invalid.Add(domain);
                continue;
            }

            if (rootOnly && !IsRootDomain(domain))
                continue;

            if (!seen.Add(domain))
            {
                if (!duplicates.Contains(domain, StringComparer.OrdinalIgnoreCase))
                    duplicates.Add(domain);
                continue;
            }

            domains.Add(domain);
        }

        domains.Sort(StringComparer.OrdinalIgnoreCase);
        duplicates.Sort(StringComparer.OrdinalIgnoreCase);
        invalid.Sort(StringComparer.OrdinalIgnoreCase);

        return new ParseResult(domains, duplicates, invalid);
    }

    public static int Count(string text)
    {
        var matches = ExtractPotentialDomains(text);
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in matches)
        {
            var domain = NormalizeDomain(match);
            if (!string.IsNullOrEmpty(domain) && HasValidTld(domain))
                unique.Add(domain);
        }

        return unique.Count;
    }

    private static List<string> ExtractPotentialDomains(string text)
    {
        var results = new List<string>();
        var tokens = text.Split(TokenDelimiters, StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in tokens)
        {
            var cleaned = StripUrlWrapper(raw);
            cleaned = RepairCommonMojibake(cleaned);
            if (string.IsNullOrWhiteSpace(cleaned) || !cleaned.Contains('.'))
                continue;

            if (IsUnicode(cleaned))
            {
                try
                {
                    var encoded = EncodeDomain(cleaned);
                    if (encoded.Contains('.'))
                        results.Add(encoded);
                }
                catch
                {
                    // Invalid IDN, skip
                }
            }
        }

        foreach (Match match in AsciiDomainRegex().Matches(text))
        {
            var candidate = match.Groups[1].Value;
            if (candidate.Length == 0)
                continue;

            var start = match.Groups[1].Index;
            if (start > 0)
            {
                var previous = text[start - 1];
                if (char.IsLetterOrDigit(previous) || previous > 127)
                    continue;
            }

            results.Add(candidate);
        }

        return results;
    }

    private static string StripUrlWrapper(string token)
    {
        var s = token;

        var protoIdx = s.IndexOf("://", StringComparison.Ordinal);
        if (protoIdx >= 0)
            s = s[(protoIdx + 3)..];
        else if (s.StartsWith("//", StringComparison.Ordinal))
            s = s[2..];

        var atIdx = s.IndexOf('@');
        if (atIdx >= 0)
            s = s[(atIdx + 1)..];

        var slashIdx = s.IndexOf('/');
        if (slashIdx >= 0)
            s = s[..slashIdx];

        var queryIdx = s.IndexOf('?');
        if (queryIdx >= 0)
            s = s[..queryIdx];

        var fragIdx = s.IndexOf('#');
        if (fragIdx >= 0)
            s = s[..fragIdx];

        var colonIdx = s.LastIndexOf(':');
        if (colonIdx >= 0 && colonIdx + 1 < s.Length && s[(colonIdx + 1)..].All(char.IsDigit))
            s = s[..colonIdx];

        return s.Trim('.', '-', '_');
    }

    private static string NormalizeDomain(string domain)
    {
        var normalized = RepairCommonMojibake(domain.Trim()).ToLowerInvariant();

        if (normalized.EndsWith('.'))
            normalized = normalized[..^1];

        if (IsUnicode(normalized))
        {
            try
            {
                normalized = EncodeDomain(normalized);
            }
            catch
            {
                return string.Empty;
            }
        }

        return normalized;
    }

    private static string RepairCommonMojibake(string text)
    {
        if (!LooksLikeMojibake(text))
            return text;

        var best = text;
        foreach (var encoding in GetCandidateEncodings(text))
        {
            var candidate = TryDecodeMojibake(text, encoding);
            if (IsBetterDomainCandidate(best, candidate))
                best = candidate;
        }

        return best;
    }

    private static IEnumerable<Encoding> GetCandidateEncodings(string text)
    {
        var prefersLatin1 = text.IndexOfAny(['\u00C3', '\u00C2', '\u00D0', '\u00D1']) >= 0;
        var prefersWindows1251 = text.IndexOfAny(['\u0420', '\u0421']) >= 0;

        if (prefersLatin1)
            yield return Latin1;

        if (prefersWindows1251)
            yield return Windows1251;

        if (!prefersLatin1)
            yield return Latin1;

        if (!prefersWindows1251)
            yield return Windows1251;
    }

    private static string TryDecodeMojibake(string text, Encoding sourceEncoding)
    {
        try
        {
            var bytes = sourceEncoding.GetBytes(text);
            return Utf8Strict.GetString(bytes);
        }
        catch
        {
            return text;
        }
    }

    private static bool IsBetterDomainCandidate(string current, string candidate)
    {
        if (candidate == current || !candidate.Contains('.'))
            return false;

        return ScoreDomainText(candidate) > ScoreDomainText(current);
    }

    private static int ScoreDomainText(string text)
    {
        var score = 0;
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c is '.' or '-')
                score += 2;

            if (IsLikelyMojibakeChar(c))
                score -= 6;

            if (c is '\uFFFD' or '?')
                score -= 10;
        }

        foreach (var label in text.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var hasLatin = label.Any(IsLatinLetter);
            var hasCyrillic = label.Any(IsCyrillicLetter);
            if (hasLatin && hasCyrillic)
                score -= 12;
        }

        return score;
    }

    private static bool LooksLikeMojibake(string text) => text.Any(IsLikelyMojibakeChar);

    private static bool IsLikelyMojibakeChar(char c) => c is '\u0420' or '\u0421' or '\u00D0' or '\u00D1' or '\u00C3' or '\u00C2';

    private static bool IsLatinLetter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' || c is >= '\u00C0' and <= '\u024F';

    private static bool IsCyrillicLetter(char c) => c is >= '\u0400' and <= '\u04FF';

    private static bool HasValidTld(string domain)
    {
        var tld = domain.Split('.')[^1];
        return tld.Any(char.IsLetter);
    }

    private static bool IsRootDomain(string domain)
    {
        var parts = domain.Split('.');

        if (parts.Length == 3 && SpecialSLDs.Contains(parts[1]))
            return true;

        return parts.Length == 2;
    }

    private static bool IsUnicode(string text) => text.Any(c => c > 127);

    private static string EncodeDomain(string domain)
    {
        try
        {
            return Idn.GetAscii(domain.ToLowerInvariant());
        }
        catch (ArgumentException)
        {
            return domain.ToLowerInvariant();
        }
    }
}
