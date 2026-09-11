using System.Text;
using CFTools.Models;

namespace CFTools.Services;

public sealed record BatchResultRow(string Domain, string Status, string? Error);

/// <summary>
/// Pure CSV assembly (RFC 4180 quoting). No file I/O here so it stays testable.
/// </summary>
public static class CsvBuilder
{
    public const string NewLine = "\n";

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var needsQuotes =
            value.Contains(',')
            || value.Contains('"')
            || value.Contains('\n')
            || value.Contains('\r');
        if (!needsQuotes)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static string Line(params string?[] fields) => string.Join(",", fields.Select(Escape));

    public static string Build(string header, IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        sb.Append(header).Append(NewLine);
        foreach (var line in lines)
            sb.Append(line).Append(NewLine);
        return sb.ToString();
    }

    /// <summary>
    /// Zones of a single account: domain,status,plan,name_servers
    /// </summary>
    public static string ZonesCsv(IEnumerable<CfZone> zones)
    {
        return Build(
            "domain,status,plan,name_servers",
            zones.Select(z => Line(z.Name, z.Status, PlanName(z), NameServers(z)))
        );
    }

    /// <summary>
    /// Zones across accounts: domain,status,account,id,plan,name_servers
    /// </summary>
    public static string ZonesCsv(IEnumerable<(CfZone Zone, string AccountName)> zones)
    {
        return Build(
            "domain,status,account,id,plan,name_servers",
            zones.Select(x =>
                Line(
                    x.Zone.Name,
                    x.Zone.Status,
                    x.AccountName,
                    x.Zone.Id,
                    PlanName(x.Zone),
                    NameServers(x.Zone)
                )
            )
        );
    }

    /// <summary>
    /// Batch outcome: domain,status,error
    /// </summary>
    public static string BatchResultsCsv(IEnumerable<BatchResultRow> rows)
    {
        return Build("domain,status,error", rows.Select(r => Line(r.Domain, r.Status, r.Error)));
    }

    private static string PlanName(CfZone zone) =>
        string.IsNullOrEmpty(zone.Plan?.Name) ? "free" : zone.Plan.Name;

    private static string NameServers(CfZone zone) =>
        zone.NameServers is { Length: > 0 } ? string.Join(" ", zone.NameServers) : string.Empty;
}
