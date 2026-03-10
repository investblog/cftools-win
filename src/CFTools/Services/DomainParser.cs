using System.Globalization;
using System.Text.RegularExpressions;

namespace CFTools.Services;

public record ParseResult(List<string> Domains, List<string> Duplicates, List<string> Invalid);

/// <summary>
/// Extracts and validates domain names from arbitrary text input.
/// Best-effort extraction — final validation is done by Cloudflare API.
/// </summary>
public static partial class DomainParser
{
    // Special second-level domains (heuristic, not PSL)
    private static readonly HashSet<string> SpecialSLDs = new(StringComparer.OrdinalIgnoreCase)
    {
        "co", "com", "net", "org", "edu", "gov", "ac", "me"
    };

    private static readonly IdnMapping Idn = new();

    [GeneratedRegex(
        @"\b((?=[a-z0-9-]{1,63}\.)(?:xn--)?[a-z0-9]+(?:-[a-z0-9]+)*\.)+(?:xn--)?[a-z0-9-]{2,63}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AsciiDomainRegex();

    /// <summary>
    /// Parse domains from arbitrary text input.
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

            // Skip invalid TLDs (filters out IP addresses)
            if (!HasValidTld(domain))
            {
                if (!invalid.Contains(domain, StringComparer.OrdinalIgnoreCase))
                    invalid.Add(domain);
                continue;
            }

            // Skip subdomains if rootOnly
            if (rootOnly && !IsRootDomain(domain))
                continue;

            // Track duplicates
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
    // Private Methods
    // ========================================================================

    private static List<string> ExtractPotentialDomains(string text)
    {
        var results = new List<string>();

        // Extract ASCII domains
        foreach (Match m in AsciiDomainRegex().Matches(text))
        {
            results.Add(m.Value);
        }

        // Extract Unicode domains (line by line)
        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(new[] { ' ', '\t', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (!IsUnicode(part) || !part.Contains('.'))
                    continue;

                try
                {
                    var encoded = EncodeDomain(part.Trim());
                    if (encoded != part.ToLowerInvariant() && encoded.Contains('.'))
                        results.Add(encoded);
                }
                catch
                {
                    // Invalid IDN, skip
                }
            }
        }

        return results;
    }

    private static string NormalizeDomain(string domain)
    {
        var normalized = domain.Trim().ToLowerInvariant();

        // Remove trailing dot
        if (normalized.EndsWith('.'))
            normalized = normalized[..^1];

        // Convert Unicode to punycode
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
