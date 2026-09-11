using System.Text.Json.Serialization;

namespace CFTools.Models;

// ============================================================================
// Credential kinds
// ============================================================================

/// <summary>
/// Three ways to authenticate against the Cloudflare API:
/// - Global API Key (legacy 37-hex or cfk_ prefix): X-Auth-Email + X-Auth-Key, needs email.
/// - User API token (cfut_): Authorization: Bearer.
/// - Account-owned token (cfat_): Authorization: Bearer, scoped to a single account.
/// </summary>
public enum CredentialKind
{
    GlobalKey,
    UserToken,
    AccountToken,
}

public sealed record CfCredential(
    CredentialKind Kind,
    string Secret,
    string? Email = null,
    string? AccountId = null
)
{
    public bool IsToken => Kind != CredentialKind.GlobalKey;
}

/// <summary>
/// Non-secret facts learned while verifying a credential.
/// </summary>
public sealed record CredentialIdentity(string Label, string? Email, string? TokenId);

public record CfTokenVerifyResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("expires_on")] string? ExpiresOn
);

public static class CredentialDetector
{
    /// <summary>
    /// Detect the credential kind from the secret itself. New-format secrets carry a
    /// prefix (cfk_ / cfut_ / cfat_); a bare 37-char lowercase-hex string is the legacy
    /// Global API Key. Anything else (including legacy unprefixed tokens) is null.
    /// </summary>
    public static CredentialKind? Detect(string? secret)
    {
        var s = secret?.Trim() ?? string.Empty;
        if (s.StartsWith("cfut_", StringComparison.Ordinal))
            return CredentialKind.UserToken;
        if (s.StartsWith("cfat_", StringComparison.Ordinal))
            return CredentialKind.AccountToken;
        if (s.StartsWith("cfk_", StringComparison.Ordinal))
            return CredentialKind.GlobalKey;
        if (s.Length == 37 && s.All(IsLowerHex))
            return CredentialKind.GlobalKey;
        return null;
    }

    /// <summary>
    /// Resolve the kind to use: detected from the secret, else Global Key when an email
    /// was supplied (legacy key), else a user token (legacy unprefixed token).
    /// </summary>
    public static CredentialKind Resolve(string? secret, string? email)
    {
        return Detect(secret)
            ?? (
                string.IsNullOrWhiteSpace(email)
                    ? CredentialKind.UserToken
                    : CredentialKind.GlobalKey
            );
    }

    public static string Describe(CredentialKind kind) =>
        kind switch
        {
            CredentialKind.GlobalKey => "Global API Key",
            CredentialKind.UserToken => "User API token",
            CredentialKind.AccountToken => "Account-owned API token",
            _ => "Credential",
        };

    /// <summary>
    /// Human-readable label for the signed-in identity (never contains the secret).
    /// </summary>
    public static string DefaultLabel(CredentialKind kind, string? email, string? tokenId)
    {
        var id8 = tokenId is { Length: > 0 } ? tokenId[..Math.Min(8, tokenId.Length)] : "";
        return kind switch
        {
            CredentialKind.GlobalKey => email ?? "Global API Key",
            CredentialKind.AccountToken => $"Account token {id8}".Trim(),
            _ => $"API token {id8}".Trim(),
        };
    }

    private static bool IsLowerHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
}

/// <summary>
/// Encodes the credential kind into the Credential Manager entry's user name, so the secret
/// can be re-attached to the right auth scheme on the next launch:
/// - Global API Key: user name = account e-mail (legacy entries, unchanged)
/// - User token:     user name = "token:user"
/// - Account token:  user name = "token:account[:accountId]"
/// </summary>
public static class CredentialVaultCodec
{
    public const string UserTokenMarker = "token:user";
    public const string AccountTokenMarker = "token:account";

    /// <summary>User name for the vault entry, or null when the credential cannot be stored.</summary>
    public static string? EncodeUserName(CfCredential credential)
    {
        var userName = credential.Kind switch
        {
            CredentialKind.UserToken => UserTokenMarker,
            CredentialKind.AccountToken => string.IsNullOrWhiteSpace(credential.AccountId)
                ? AccountTokenMarker
                : $"{AccountTokenMarker}:{credential.AccountId}",
            _ => credential.Email ?? string.Empty,
        };

        return string.IsNullOrEmpty(userName) ? null : userName;
    }

    public static CfCredential Decode(string userName, string secret)
    {
        if (userName == UserTokenMarker)
            return new CfCredential(CredentialKind.UserToken, secret);

        if (userName.StartsWith(AccountTokenMarker, StringComparison.Ordinal))
        {
            var accountId =
                userName.Length > AccountTokenMarker.Length + 1
                    ? userName[(AccountTokenMarker.Length + 1)..]
                    : null;
            return new CfCredential(CredentialKind.AccountToken, secret, AccountId: accountId);
        }

        // Legacy entry: the user name is the e-mail that goes with a Global API Key.
        return new CfCredential(CredentialKind.GlobalKey, secret, Email: userName);
    }
}
