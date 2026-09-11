using CFTools.Models;
using CFTools.Services;
using Xunit;

namespace CFTools.Tests;

public class CsvBuilderTests
{
    private static CfZone Zone(
        string name,
        string status = "active",
        string? plan = "Free Website"
    ) =>
        new(
            "zone-" + name,
            name,
            status,
            "full",
            new[] { "ns1.example.com", "ns2.example.com" },
            new CfAccount("acc1", "Main"),
            plan is null ? null! : new CfPlan("plan", plan, "free")
        );

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("has,comma", "\"has,comma\"")]
    [InlineData("has \"quote\"", "\"has \"\"quote\"\"\"")]
    [InlineData("multi\nline", "\"multi\nline\"")]
    public void Escape_QuotesOnlyWhenNeeded(string? input, string expected)
    {
        Assert.Equal(expected, CsvBuilder.Escape(input));
    }

    [Fact]
    public void ZonesCsv_SingleAccount_HasExpectedColumns()
    {
        var csv = CsvBuilder.ZonesCsv(new[] { Zone("example.com") });

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("domain,status,plan,name_servers", lines[0]);
        Assert.Equal("example.com,active,Free Website,ns1.example.com ns2.example.com", lines[1]);
    }

    [Fact]
    public void ZonesCsv_AllAccounts_IncludesAccountAndId()
    {
        var csv = CsvBuilder.ZonesCsv(new[] { (Zone("example.com"), "Main, Inc") });

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("domain,status,account,id,plan,name_servers", lines[0]);
        Assert.Equal(
            "example.com,active,\"Main, Inc\",zone-example.com,Free Website,ns1.example.com ns2.example.com",
            lines[1]
        );
    }

    [Fact]
    public void ZonesCsv_MissingPlan_DefaultsToFree()
    {
        var csv = CsvBuilder.ZonesCsv(new[] { Zone("example.com", plan: null) });

        Assert.Contains("example.com,active,free,", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void BatchResultsCsv_EscapesErrorMessages()
    {
        var csv = CsvBuilder.BatchResultsCsv(
            new[]
            {
                new BatchResultRow("a.com", "created", null),
                new BatchResultRow("b.com", "failed", "Zone \"b.com\" already exists, code 1061"),
            }
        );

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("domain,status,error", lines[0]);
        Assert.Equal("a.com,created,", lines[1]);
        Assert.Equal("b.com,failed,\"Zone \"\"b.com\"\" already exists, code 1061\"", lines[2]);
    }
}
