namespace CFTools.Models;

// ============================================================================
// Error Categories
// ============================================================================

public enum ErrorCategory
{
    Auth,
    RateLimit,
    Validation,
    Dependency,
    Network,
    Unknown,
}

// ============================================================================
// Normalized Error
// ============================================================================

public record NormalizedError(
    ErrorCategory Category,
    int Code,
    string Message,
    string Recommendation,
    bool Retryable,
    int? RetryAfterMs = null
);

// ============================================================================
// CF API Exception
// ============================================================================

public class CfApiException : Exception
{
    public NormalizedError Normalized { get; }
    public int? RetryAfterMs => Normalized.RetryAfterMs;

    public CfApiException(NormalizedError normalized)
        : base(normalized.Message)
    {
        Normalized = normalized;
    }
}

// ============================================================================
// Error Normalizer
// ============================================================================

public static class ErrorNormalizer
{
    // Known Cloudflare auth error codes
    private static readonly HashSet<int> AuthErrorCodes = new()
    {
        10000, // Invalid credentials
        10001, // Invalid token
        6003, // Invalid request headers
        6100, // Invalid auth key format
        6101, // Invalid auth email format
        6102, // Missing auth email
        6103, // Missing auth key
        9103, // Unknown auth key
        9106, // Invalid auth header
    };

    public static NormalizedError Normalize(
        int code,
        string message,
        string? retryAfterHeader = null
    )
    {
        int? retryAfterMs = null;
        if (retryAfterHeader is not null && int.TryParse(retryAfterHeader, out var seconds))
        {
            retryAfterMs = seconds * 1000;
        }

        // Auth errors
        if (AuthErrorCodes.Contains(code))
        {
            return new NormalizedError(
                ErrorCategory.Auth,
                code,
                message,
                "Check your email and Global API Key",
                Retryable: false
            );
        }

        // Rate limit
        if (code == 429)
        {
            return new NormalizedError(
                ErrorCategory.RateLimit,
                code,
                string.IsNullOrEmpty(message) ? "Rate limited" : message,
                "Waiting for rate limit to reset...",
                Retryable: true,
                RetryAfterMs: retryAfterMs ?? 60_000
            );
        }

        // Zone already exists
        if (code == 1061)
        {
            return new NormalizedError(
                ErrorCategory.Validation,
                code,
                message,
                "Zone already exists in this account",
                Retryable: false
            );
        }

        // Invalid zone name
        if (code == 1003)
        {
            return new NormalizedError(
                ErrorCategory.Validation,
                code,
                message,
                "Invalid zone name",
                Retryable: false
            );
        }

        // Zone has subscription
        if (code == 1099)
        {
            return new NormalizedError(
                ErrorCategory.Dependency,
                code,
                message,
                "Remove subscriptions in Cloudflare Dashboard first",
                Retryable: false
            );
        }

        // Server errors (5xx)
        if (code >= 500 && code < 600)
        {
            return new NormalizedError(
                ErrorCategory.Network,
                code,
                string.IsNullOrEmpty(message) ? "Server error" : message,
                "Retrying automatically...",
                Retryable: true
            );
        }

        // Unknown
        return new NormalizedError(
            ErrorCategory.Unknown,
            code,
            message,
            "An unexpected error occurred",
            Retryable: false
        );
    }

    public static NormalizedError NetworkError(string message)
    {
        return new NormalizedError(
            ErrorCategory.Network,
            0,
            message,
            "Check your internet connection",
            Retryable: true
        );
    }

    public static NormalizedError TimeoutError(int timeoutMs)
    {
        return new NormalizedError(
            ErrorCategory.Network,
            0,
            $"Request timed out after {timeoutMs}ms",
            "Retrying automatically...",
            Retryable: true
        );
    }
}
