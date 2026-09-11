using CFTools.Models;
using Xunit;

namespace CFTools.Tests;

public class CredentialVaultCodecTests
{
    [Fact]
    public void LegacyEntry_EmailUserName_DecodesAsGlobalKey()
    {
        // Entries written by 1.1.x store the e-mail as the user name and the key as the password.
        var credential = CredentialVaultCodec.Decode("user@example.com", "0123abcd");

        Assert.Equal(CredentialKind.GlobalKey, credential.Kind);
        Assert.Equal("user@example.com", credential.Email);
        Assert.Equal("0123abcd", credential.Secret);
        Assert.Null(credential.AccountId);
    }

    [Fact]
    public void GlobalKey_RoundTrip_KeepsLegacyShape()
    {
        var original = new CfCredential(
            CredentialKind.GlobalKey,
            "secret",
            Email: "user@example.com"
        );

        var userName = CredentialVaultCodec.EncodeUserName(original);

        Assert.Equal("user@example.com", userName);
        Assert.Equal(original, CredentialVaultCodec.Decode(userName!, "secret"));
    }

    [Fact]
    public void GlobalKey_WithoutEmail_CannotBeStored()
    {
        Assert.Null(
            CredentialVaultCodec.EncodeUserName(
                new CfCredential(CredentialKind.GlobalKey, "secret")
            )
        );
    }

    [Fact]
    public void UserToken_RoundTrip()
    {
        var original = new CfCredential(CredentialKind.UserToken, "cfut_abc");

        var userName = CredentialVaultCodec.EncodeUserName(original);

        Assert.Equal("token:user", userName);
        Assert.Equal(original, CredentialVaultCodec.Decode(userName!, "cfut_abc"));
    }

    [Fact]
    public void AccountToken_WithAccountId_RoundTrip()
    {
        var original = new CfCredential(
            CredentialKind.AccountToken,
            "cfat_abc",
            AccountId: "d36a36cd1d5b17048d3b20a4c32aa7c7"
        );

        var userName = CredentialVaultCodec.EncodeUserName(original);

        Assert.Equal("token:account:d36a36cd1d5b17048d3b20a4c32aa7c7", userName);
        Assert.Equal(original, CredentialVaultCodec.Decode(userName!, "cfat_abc"));
    }

    [Fact]
    public void AccountToken_WithoutAccountId_RoundTrip()
    {
        var original = new CfCredential(CredentialKind.AccountToken, "cfat_abc");

        var userName = CredentialVaultCodec.EncodeUserName(original);

        Assert.Equal("token:account", userName);
        Assert.Equal(original, CredentialVaultCodec.Decode(userName!, "cfat_abc"));
    }
}
