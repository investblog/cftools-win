using Xunit;
using CFTools.Services;

namespace CFTools.Tests;

public class DomainParserTests
{
    [Fact]
    public void Parse_SimpleList_ExtractsDomains()
    {
        var input = "example.com\ntest.org\nfoo.net";
        var result = DomainParser.Parse(input);

        Assert.Equal(3, result.Domains.Count);
        Assert.Contains("example.com", result.Domains);
        Assert.Contains("test.org", result.Domains);
        Assert.Contains("foo.net", result.Domains);
    }

    [Fact]
    public void Parse_MixedText_ExtractsDomains()
    {
        var input = "Please add example.com and test.org to the account";
        var result = DomainParser.Parse(input);

        Assert.Equal(2, result.Domains.Count);
        Assert.Contains("example.com", result.Domains);
        Assert.Contains("test.org", result.Domains);
    }

    [Fact]
    public void Parse_URLs_ExtractsDomains()
    {
        var input = "https://example.com/path?q=1\nhttps://test.org/page";
        var result = DomainParser.Parse(input);

        Assert.Contains("example.com", result.Domains);
        Assert.Contains("test.org", result.Domains);
    }

    [Fact]
    public void Parse_Duplicates_Detected()
    {
        var input = "example.com\ntest.org\nexample.com";
        var result = DomainParser.Parse(input);

        Assert.Equal(2, result.Domains.Count);
        Assert.Single(result.Duplicates);
        Assert.Contains("example.com", result.Duplicates);
    }

    [Fact]
    public void Parse_CaseInsensitive_Dedup()
    {
        var input = "Example.COM\nexample.com";
        var result = DomainParser.Parse(input);

        Assert.Single(result.Domains);
        Assert.Single(result.Duplicates);
    }

    [Fact]
    public void Parse_TrailingDot_Removed()
    {
        var input = "example.com.";
        var result = DomainParser.Parse(input);

        Assert.Single(result.Domains);
        Assert.Equal("example.com", result.Domains[0]);
    }

    [Fact]
    public void Parse_SubdomainsFiltered_WhenRootOnly()
    {
        var input = "www.example.com\nsub.test.org\nexample.com";
        var result = DomainParser.Parse(input, rootOnly: true);

        Assert.Single(result.Domains);
        Assert.Equal("example.com", result.Domains[0]);
    }

    [Fact]
    public void Parse_SubdomainsKept_WhenNotRootOnly()
    {
        var input = "www.example.com\nsub.test.org\nexample.com";
        var result = DomainParser.Parse(input, rootOnly: false);

        Assert.Equal(3, result.Domains.Count);
    }

    [Fact]
    public void Parse_SpecialSLDs_TreatedAsRoot()
    {
        var input = "example.co.uk\ntest.com.br";
        var result = DomainParser.Parse(input, rootOnly: true);

        Assert.Equal(2, result.Domains.Count);
        Assert.Contains("example.co.uk", result.Domains);
        Assert.Contains("test.com.br", result.Domains);
    }

    [Fact]
    public void Parse_IPAddresses_Filtered()
    {
        var input = "192.168.1.1\nexample.com";
        var result = DomainParser.Parse(input);

        Assert.Single(result.Domains);
        Assert.Equal("example.com", result.Domains[0]);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty()
    {
        var result = DomainParser.Parse("");

        Assert.Empty(result.Domains);
        Assert.Empty(result.Duplicates);
        Assert.Empty(result.Invalid);
    }

    [Fact]
    public void Parse_PunycodeDomains_Accepted()
    {
        var input = "xn--d1acufc.xn--p1ai";
        var result = DomainParser.Parse(input);

        Assert.Single(result.Domains);
        Assert.Equal("xn--d1acufc.xn--p1ai", result.Domains[0]);
    }

    [Fact]
    public void Count_ReturnsCorrectNumber()
    {
        var input = "example.com\ntest.org\nexample.com";
        Assert.Equal(2, DomainParser.Count(input));
    }

    [Fact]
    public void Parse_CommaSeparated_ExtractsDomains()
    {
        var input = "example.com, test.org, foo.net";
        var result = DomainParser.Parse(input);

        Assert.Equal(3, result.Domains.Count);
    }
}
