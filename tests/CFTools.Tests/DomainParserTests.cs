using CFTools.Services;
using Xunit;

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
    public void Parse_CommaSeparated_ExtractsDomains()
    {
        var input = "example.com, test.org, foo.net";
        var result = DomainParser.Parse(input);

        Assert.Equal(3, result.Domains.Count);
    }

    [Fact]
    public void Parse_SemicolonAndPipeSeparated_ExtractsDomains()
    {
        var input = "example.com; test.org | foo.net";
        var result = DomainParser.Parse(input);

        Assert.Equal(3, result.Domains.Count);
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
    public void Parse_URLs_ExtractsDomains()
    {
        var input = "https://example.com/path?q=1\nhttps://test.org/page";
        var result = DomainParser.Parse(input);

        Assert.Contains("example.com", result.Domains);
        Assert.Contains("test.org", result.Domains);
    }

    [Fact]
    public void Parse_UrlWithPort_ExtractsDomain()
    {
        var input = "https://example.com:8080/path";
        var result = DomainParser.Parse(input);

        Assert.Contains("example.com", result.Domains);
    }

    [Fact]
    public void Parse_EmailAddress_ExtractsDomain()
    {
        var input = "user@example.com";
        var result = DomainParser.Parse(input);

        Assert.Contains("example.com", result.Domains);
    }

    [Fact]
    public void Parse_HtmlContent_ExtractsDomains()
    {
        var input = "<a href=\"https://example.com\">link</a> and <img src=\"https://test.org/img.png\">";
        var result = DomainParser.Parse(input);

        Assert.Contains("example.com", result.Domains);
        Assert.Contains("test.org", result.Domains);
    }

    [Fact]
    public void Parse_JsonContent_ExtractsDomains()
    {
        var input = "{\"domain\":\"example.com\",\"other\":\"test.org\"}";
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
    public void Parse_PunycodeDomains_Accepted()
    {
        var input = "xn--d1acufc.xn--p1ai";
        var result = DomainParser.Parse(input);

        Assert.Single(result.Domains);
        Assert.Equal("xn--d1acufc.xn--p1ai", result.Domains[0]);
    }

    [Fact]
    public void Parse_UnicodeDomain_ConvertsToPunycode()
    {
        var input = "домен.рф";
        var result = DomainParser.Parse(input);

        Assert.Single(result.Domains);
        Assert.Equal("xn--d1acufc.xn--p1ai", result.Domains[0]);
    }

    [Fact]
    public void Parse_MojibakeWindows1251Domain_RepairsToPunycode()
    {
        var input = "РґРѕРјРµРЅ.СЂС„";
        var result = DomainParser.Parse(input);

        Assert.Single(result.Domains);
        Assert.Equal("xn--d1acufc.xn--p1ai", result.Domains[0]);
    }

    [Fact]
    public void Parse_UnicodeDomainInUrl_ExtractsDomain()
    {
        var input = "https://домен.рф/some/path?q=1";
        var result = DomainParser.Parse(input);

        Assert.Single(result.Domains);
        Assert.Equal("xn--d1acufc.xn--p1ai", result.Domains[0]);
    }

    [Fact]
    public void Parse_MixedAsciiAndUnicode_ExtractsAll()
    {
        var input = "example.com\nдомен.рф\ntest.org";
        var result = DomainParser.Parse(input);

        Assert.Equal(3, result.Domains.Count);
        Assert.Contains("example.com", result.Domains);
        Assert.Contains("xn--d1acufc.xn--p1ai", result.Domains);
        Assert.Contains("test.org", result.Domains);
    }

    [Fact]
    public void Parse_GermanUmlautDomain_ConvertsToPunycode()
    {
        var input = "müller.de";
        var result = DomainParser.Parse(input);

        Assert.Single(result.Domains);
        Assert.Equal("xn--mller-kva.de", result.Domains[0]);
    }

    [Fact]
    public void Parse_Latin1MojibakeDomain_RepairsToPunycode()
    {
        var input = "mÃ¼ller.de";
        var result = DomainParser.Parse(input);

        Assert.Single(result.Domains);
        Assert.Equal("xn--mller-kva.de", result.Domains[0]);
    }

    [Fact]
    public void Parse_MultipleUnicodeDomainsInText()
    {
        var input = "Добавить домен.рф и сайт.рф в Cloudflare";
        var result = DomainParser.Parse(input);

        Assert.Equal(2, result.Domains.Count);
    }

    [Fact]
    public void Count_ReturnsCorrectNumber()
    {
        var input = "example.com\ntest.org\nexample.com";
        Assert.Equal(2, DomainParser.Count(input));
    }

    [Fact]
    public void Parse_MessyInput_ExtractsAll()
    {
        var input = """
            Here are the domains:
            - https://example.com/page
            - test.org, foo.net
            Also домен.рф and user@bar.com
            <a href="https://baz.io">link</a>
            """;
        var result = DomainParser.Parse(input);

        Assert.Contains("example.com", result.Domains);
        Assert.Contains("test.org", result.Domains);
        Assert.Contains("foo.net", result.Domains);
        Assert.Contains("bar.com", result.Domains);
        Assert.Contains("baz.io", result.Domains);
        Assert.Contains("xn--d1acufc.xn--p1ai", result.Domains);
        Assert.Equal(6, result.Domains.Count);
    }
}
