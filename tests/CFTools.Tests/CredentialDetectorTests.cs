using CFTools.Models;
using Xunit;

namespace CFTools.Tests;

public class CredentialDetectorTests
{
    [Theory]
    [InlineData("cfut_abc123", CredentialKind.UserToken)]
    [InlineData("  cfut_abc123  ", CredentialKind.UserToken)]
    [InlineData("cfat_abc123", CredentialKind.AccountToken)]
    [InlineData("cfk_abc123", CredentialKind.GlobalKey)]
    [InlineData("0123456789abcdef0123456789abcdef01234", CredentialKind.GlobalKey)]
    public void Detect_KnownFormats_ReturnsKind(string secret, CredentialKind expected)
    {
        Assert.Equal(expected, CredentialDetector.Detect(secret));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF01234")] // uppercase hex is not a legacy key
    [InlineData("0123456789abcdef0123456789abcdef0123")] // 36 chars
    [InlineData("legacy-unprefixed-token-value-here_1234567890")]
    public void Detect_UnknownFormats_ReturnsNull(string? secret)
    {
        Assert.Null(CredentialDetector.Detect(secret));
    }

    [Fact]
    public void Resolve_UnknownWithEmail_TreatsAsGlobalKey()
    {
        Assert.Equal(
            CredentialKind.GlobalKey,
            CredentialDetector.Resolve("unknown-format", "user@example.com")
        );
    }

    [Fact]
    public void Resolve_UnknownWithoutEmail_TreatsAsUserToken()
    {
        Assert.Equal(CredentialKind.UserToken, CredentialDetector.Resolve("unknown-format", ""));
    }

    [Fact]
    public void Resolve_DetectedKindWinsOverEmail()
    {
        Assert.Equal(
            CredentialKind.UserToken,
            CredentialDetector.Resolve("cfut_xyz", "user@example.com")
        );
    }

    [Fact]
    public void DefaultLabel_NeverContainsFullTokenId()
    {
        var label = CredentialDetector.DefaultLabel(
            CredentialKind.UserToken,
            null,
            "0123456789abcdef"
        );

        Assert.Equal("API token 01234567", label);
    }

    [Fact]
    public void DefaultLabel_GlobalKey_UsesEmail()
    {
        Assert.Equal(
            "user@example.com",
            CredentialDetector.DefaultLabel(CredentialKind.GlobalKey, "user@example.com", null)
        );
    }
}
