using CFTools.Models;
using Xunit;

namespace CFTools.Tests;

public class ErrorNormalizerTests
{
    [Theory]
    [InlineData(10000)]
    [InlineData(10001)]
    [InlineData(6003)]
    [InlineData(6100)]
    [InlineData(6101)]
    [InlineData(6102)]
    [InlineData(6103)]
    [InlineData(9103)]
    [InlineData(9106)]
    public void Normalize_AuthCodes_ReturnAuthCategory(int code)
    {
        var result = ErrorNormalizer.Normalize(code, "test error");

        Assert.Equal(ErrorCategory.Auth, result.Category);
        Assert.False(result.Retryable);
    }

    [Fact]
    public void Normalize_429_ReturnRateLimitCategory()
    {
        var result = ErrorNormalizer.Normalize(429, "Too Many Requests");

        Assert.Equal(ErrorCategory.RateLimit, result.Category);
        Assert.True(result.Retryable);
    }

    [Fact]
    public void Normalize_429_WithRetryAfter_ParsesHeader()
    {
        var result = ErrorNormalizer.Normalize(429, "Rate limited", retryAfterHeader: "30");

        Assert.Equal(ErrorCategory.RateLimit, result.Category);
        Assert.Equal(30_000, result.RetryAfterMs);
    }

    [Fact]
    public void Normalize_429_WithoutRetryAfter_DefaultsTo60s()
    {
        var result = ErrorNormalizer.Normalize(429, "Rate limited");

        Assert.Equal(60_000, result.RetryAfterMs);
    }

    [Fact]
    public void Normalize_1061_ReturnValidationCategory()
    {
        var result = ErrorNormalizer.Normalize(1061, "Zone already exists");

        Assert.Equal(ErrorCategory.Validation, result.Category);
        Assert.False(result.Retryable);
    }

    [Fact]
    public void Normalize_1003_ReturnValidationCategory()
    {
        var result = ErrorNormalizer.Normalize(1003, "Invalid zone name");

        Assert.Equal(ErrorCategory.Validation, result.Category);
        Assert.False(result.Retryable);
    }

    [Fact]
    public void Normalize_1099_ReturnDependencyCategory()
    {
        var result = ErrorNormalizer.Normalize(1099, "Zone has subscription");

        Assert.Equal(ErrorCategory.Dependency, result.Category);
        Assert.False(result.Retryable);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void Normalize_5xx_ReturnNetworkCategory(int code)
    {
        var result = ErrorNormalizer.Normalize(code, "Server error");

        Assert.Equal(ErrorCategory.Network, result.Category);
        Assert.True(result.Retryable);
    }

    [Fact]
    public void Normalize_UnknownCode_ReturnUnknownCategory()
    {
        var result = ErrorNormalizer.Normalize(9999, "Something weird");

        Assert.Equal(ErrorCategory.Unknown, result.Category);
        Assert.False(result.Retryable);
    }

    [Fact]
    public void NetworkError_ReturnRetryable()
    {
        var result = ErrorNormalizer.NetworkError("Connection refused");

        Assert.Equal(ErrorCategory.Network, result.Category);
        Assert.True(result.Retryable);
    }

    [Fact]
    public void TimeoutError_ReturnRetryable()
    {
        var result = ErrorNormalizer.TimeoutError(30000);

        Assert.Equal(ErrorCategory.Network, result.Category);
        Assert.True(result.Retryable);
        Assert.Contains("30000", result.Message);
    }
}
