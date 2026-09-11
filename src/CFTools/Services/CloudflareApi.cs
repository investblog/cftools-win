using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CFTools.Models;

namespace CFTools.Services;

/// <summary>
/// Cloudflare API v4 client. All requests go through this class.
/// </summary>
public sealed class CloudflareApi : IDisposable
{
    private const string BaseUrl = "https://api.cloudflare.com/client/v4/";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _http;
    private CfCredential? _credential;

    public bool IsConfigured => _credential is not null;

    public CfCredential? Credential => _credential;

    public CloudflareApi()
    {
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = DefaultTimeout };
    }

    public void SetCredentials(CfCredential credential)
    {
        _credential = credential;

        _http.DefaultRequestHeaders.Clear();
        if (credential.Kind == CredentialKind.GlobalKey)
        {
            _http.DefaultRequestHeaders.Add("X-Auth-Email", credential.Email ?? string.Empty);
            _http.DefaultRequestHeaders.Add("X-Auth-Key", credential.Secret);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                credential.Secret
            );
        }
    }

    public void ClearCredentials()
    {
        _credential = null;
        _http.DefaultRequestHeaders.Clear();
    }

    // ========================================================================
    // API Methods
    // ========================================================================

    /// <summary>
    /// Verify the configured credential, routed by kind:
    /// global-key → GET user; user-token → GET user/tokens/verify;
    /// account-token → GET accounts/{id}/tokens/verify (account discovered via GET accounts
    /// when the credential does not name one).
    /// </summary>
    public async Task<CredentialIdentity> VerifyCredentials(CancellationToken ct = default)
    {
        EnsureConfigured();
        var credential = _credential!;

        if (credential.Kind == CredentialKind.GlobalKey)
        {
            var user = await Get<CfUser>("user", ct);
            return new CredentialIdentity(user.Email, user.Email, null);
        }

        string path;
        if (credential.Kind == CredentialKind.UserToken)
        {
            path = "user/tokens/verify";
        }
        else
        {
            var accountId = credential.AccountId;
            if (string.IsNullOrWhiteSpace(accountId))
            {
                var accounts = await GetAccounts(ct);
                accountId =
                    accounts.FirstOrDefault()?.Id
                    ?? throw new CfApiException(
                        ErrorNormalizer.Normalize(
                            10001,
                            "Account-owned token: no account visible. Enter the Account ID."
                        )
                    );
            }
            path = $"accounts/{accountId}/tokens/verify";
        }

        var result = await Get<CfTokenVerifyResult>(path, ct);
        if (!string.Equals(result.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new CfApiException(
                ErrorNormalizer.Normalize(10001, $"Token status is \"{result.Status}\"")
            );
        }

        return new CredentialIdentity(
            CredentialDetector.DefaultLabel(credential.Kind, null, result.Id),
            null,
            result.Id
        );
    }

    /// <summary>
    /// All accounts visible to the credential (every page; CF caps per_page at 50).
    /// </summary>
    public async Task<List<CfAccount>> GetAccounts(CancellationToken ct = default)
    {
        const int maxPages = 40; // safety cap: 40 * 50 = 2k accounts
        var all = new List<CfAccount>();
        var page = 1;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var result = await GetPaginated<CfAccount>($"accounts?page={page}&per_page=50", ct);
            all.AddRange(result.Items);

            if (result.Items.Count == 0 || page >= result.Pagination.TotalPages)
                break;
            if (page >= maxPages)
            {
                throw new CfApiException(
                    ErrorNormalizer.NetworkError(
                        $"More than {maxPages * 50} accounts; account listing aborted"
                    )
                );
            }
            page++;
        }

        return all;
    }

    public async Task<PaginatedResult<CfZone>> ListZones(
        string? accountId = null,
        string? name = null,
        int page = 1,
        int perPage = 50,
        CancellationToken ct = default
    )
    {
        var query = new List<string> { $"page={page}", $"per_page={perPage}" };
        if (accountId is not null)
            query.Add($"account.id={accountId}");
        if (name is not null)
            query.Add($"name={Uri.EscapeDataString(name)}");

        var endpoint = $"zones?{string.Join("&", query)}";
        return await GetPaginated<CfZone>(endpoint, ct);
    }

    public async Task<List<CfZone>> ListAllZones(string accountId, CancellationToken ct = default)
    {
        var allZones = new List<CfZone>();
        var page = 1;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var result = await ListZones(accountId, page: page, ct: ct);
            allZones.AddRange(result.Items);

            if (page >= result.Pagination.TotalPages)
                break;
            page++;
        }

        return allZones;
    }

    public async Task<(bool Exists, string? ZoneId)> CheckZoneExists(
        string domain,
        string? accountId = null,
        CancellationToken ct = default
    )
    {
        var result = await ListZones(accountId: accountId, name: domain, perPage: 1, ct: ct);
        if (result.Items.Count > 0)
            return (true, result.Items[0].Id);
        return (false, null);
    }

    public async Task<CfZone> CreateZone(
        string domain,
        string accountId,
        string type = "full",
        bool jumpStart = true,
        CancellationToken ct = default
    )
    {
        var request = new CreateZoneRequest(domain, new AccountRef(accountId), type, jumpStart);
        return await Post<CfZone>("zones", request, ct);
    }

    public async Task<string> PurgeCacheEverything(string zoneId, CancellationToken ct = default)
    {
        var request = new PurgeCacheRequest(PurgeEverything: true);
        var result = await Post<PurgeCacheResponse>($"zones/{zoneId}/purge_cache", request, ct);
        return result.Id;
    }

    public async Task DeleteZone(string zoneId, CancellationToken ct = default)
    {
        await Delete($"zones/{zoneId}", ct);
    }

    // ========================================================================
    // Private HTTP Methods
    // ========================================================================

    private async Task<T> Get<T>(string endpoint, CancellationToken ct)
    {
        EnsureConfigured();

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(endpoint, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new CfApiException(ErrorNormalizer.NetworkError(ex.Message));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CfApiException(
                ErrorNormalizer.TimeoutError((int)DefaultTimeout.TotalMilliseconds)
            );
        }

        return await HandleResponse<T>(response);
    }

    private async Task<PaginatedResult<T>> GetPaginated<T>(string endpoint, CancellationToken ct)
    {
        EnsureConfigured();

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(endpoint, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new CfApiException(ErrorNormalizer.NetworkError(ex.Message));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CfApiException(
                ErrorNormalizer.TimeoutError((int)DefaultTimeout.TotalMilliseconds)
            );
        }

        var data =
            await response.Content.ReadFromJsonAsync<ApiResponse<List<T>>>(JsonOptions, ct)
            ?? throw new CfApiException(ErrorNormalizer.NetworkError("Empty response from API"));

        if (!data.Success)
            ThrowApiError(data.Errors, response);

        var pagination =
            data.ResultInfo
            ?? new PaginationInfo(1, data.Result.Count, data.Result.Count, data.Result.Count, 1);
        return new PaginatedResult<T>(data.Result, pagination);
    }

    private async Task<T> Post<T>(string endpoint, object body, CancellationToken ct)
    {
        EnsureConfigured();

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(endpoint, body, JsonOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new CfApiException(ErrorNormalizer.NetworkError(ex.Message));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CfApiException(
                ErrorNormalizer.TimeoutError((int)DefaultTimeout.TotalMilliseconds)
            );
        }

        return await HandleResponse<T>(response);
    }

    private async Task Delete(string endpoint, CancellationToken ct)
    {
        EnsureConfigured();

        HttpResponseMessage response;
        try
        {
            response = await _http.DeleteAsync(endpoint, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new CfApiException(ErrorNormalizer.NetworkError(ex.Message));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CfApiException(
                ErrorNormalizer.TimeoutError((int)DefaultTimeout.TotalMilliseconds)
            );
        }

        // Delete returns {success: true, result: {id: "..."}} — just check success
        var data =
            await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions)
            ?? throw new CfApiException(ErrorNormalizer.NetworkError("Empty response from API"));

        if (!data.Success)
            ThrowApiError(data.Errors, response);
    }

    private async Task<T> HandleResponse<T>(HttpResponseMessage response)
    {
        var data =
            await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions)
            ?? throw new CfApiException(ErrorNormalizer.NetworkError("Empty response from API"));

        if (!data.Success)
            ThrowApiError(data.Errors, response);

        return data.Result;
    }

    private static void ThrowApiError(List<ApiError> errors, HttpResponseMessage response)
    {
        var error = errors.FirstOrDefault();
        var code = error?.Code ?? (int)response.StatusCode;

        // Extract detailed message from error_chain if available
        var message =
            error?.ErrorChain?.FirstOrDefault()?.Message ?? error?.Message ?? "Unknown error";

        var retryAfter =
            response.Headers.RetryAfter?.Delta?.TotalSeconds.ToString()
            ?? response
                .Headers.RetryAfter?.Date?.Subtract(DateTimeOffset.UtcNow)
                .TotalSeconds.ToString();

        var normalized = ErrorNormalizer.Normalize(code, message, retryAfter);
        throw new CfApiException(normalized);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "Credentials not configured. Call SetCredentials first."
            );
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
