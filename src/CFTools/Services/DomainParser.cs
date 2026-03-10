using System.Globalization;
using System.Text.RegularExpressions;

namespace CFTools.Services;

public record ParseResult(List<string> Domains, List<string> Duplicates, List<string> Invalid);

/// <summary>
/// Extracts and validates domain names from arbitrary text input.
/// Handles URLs, HTML, JSON, CSV, email addresses, IDN/punycode.
/// Best-effort extraction — final validation is done by Cloudflare API.
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

    [GeneratedRegex(
        @"\b((?=[a-z0-9-]{1,63}\.)(?:xn--)?[a-z0-9]+(?:-[a-z0-9]+)*\.)+(?:xn--)?[a-z0-9-]{2,63}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    )]
    private static partial Regex AsciiDomainRegex();

    /// <summary>
    /// Parse domains from arbitrary text input.
    /// Accepts any text: plain lists, URLs, HTML, JSON, CSV, mixed prose.
    /// </summary>
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

    /// <summary>
    /// Quick count of unique domains in text.
    /// </summary>
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

    // ========================================================================
    // Extraction
    // ========================================================================

    private static List<string> ExtractPotentialDomains(string text)
    {
        var results = new List<string>();

        // Phase 1: Tokenize and extract Unicode domains (regex can't match these)
        var tokens = text.Split(TokenDelimiters, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in tokens)
        {
            var cleaned = StripUrlWrapper(raw);
            if (string.IsNullOrWhiteSpace(cleaned) || !cleaned.Contains('.'))
                continue;

            if (!IsUnicode(cleaned))
                continue;

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

        // Phase 2: Full-text ASCII regex (handles domains in any context —
        // URLs, HTML attrs, JSON values, emails, ports — via word boundaries)
        foreach (Match m in AsciiDomainRegex().Matches(text))
        {
            results.Add(m.Value);
        }

        return results;
    }

    /// <summary>
    /// Strip URL protocol, path, query, fragment, port, and email prefix
    /// from a token to expose the bare hostname.
    /// </summary>
    private static string StripUrlWrapper(string token)
    {
        var s = token;

        // Strip protocol (https://, http://, ftp://, //)
        var protoIdx = s.IndexOf("://", StringComparison.Ordinal);
        if (protoIdx >= 0)
            s = s[(protoIdx + 3)..];
        else if (s.StartsWith("//", StringComparison.Ordinal))
            s = s[2..];

        // Strip email prefix (user@)
        var atIdx = s.IndexOf('@');
        if (atIdx >= 0)
            s = s[(atIdx + 1)..];

        // Strip path
        var slashIdx = s.IndexOf('/');
        if (slashIdx >= 0)
            s = s[..slashIdx];

        // Strip query string
        var queryIdx = s.IndexOf('?');
        if (queryIdx >= 0)
            s = s[..queryIdx];

        // Strip fragment
        var fragIdx = s.IndexOf('#');
        if (fragIdx >= 0)
            s = s[..fragIdx];

        // Strip port (:1234)
        var colonIdx = s.LastIndexOf(':');
        if (colonIdx >= 0 && colonIdx + 1 < s.Length && s[(colonIdx + 1)..].All(char.IsDigit))
            s = s[..colonIdx];

        // Strip surrounding noise
        s = s.Trim('.', '-', '_');

        return s;
    }

    // ========================================================================
    // Normalization & Validation
    // ========================================================================

    private static string NormalizeDomain(string domain)
    {
        var normalized = domain.Trim().ToLowerInvariant();

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

    private static bool HasValidTld(string domain)
    {
        var tld = domain.Split('.')[^1];
        return tld.Any(char.IsLetter);
    }

    private static bool IsRootDomain(string domain)
    {
        var parts = domain.Split('.');

        // Handle special SLDs like .co.uk, .com.br
        if (parts.Length == 3 && SpecialSLDs.Contains(parts[1]))
            return true;

        return parts.Length == 2;
    }

    private static bool IsUnicode(string text)
    {
        return text.Any(c => c > 127);
    }

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
