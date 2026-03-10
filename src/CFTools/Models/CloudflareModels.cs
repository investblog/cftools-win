using System.Text.Json.Serialization;

namespace CFTools.Models;

// ============================================================================
// API Response Wrapper
// ============================================================================

public record ApiResponse<T>(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("errors")] List<ApiError> Errors,
    [property: JsonPropertyName("messages")] List<string> Messages,
    [property: JsonPropertyName("result")] T Result,
    [property: JsonPropertyName("result_info")] PaginationInfo? ResultInfo
);

public record ApiError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("error_chain")] List<ApiError>? ErrorChain
);

public record PaginationInfo(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("per_page")] int PerPage,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("total_count")] int TotalCount,
    [property: JsonPropertyName("total_pages")] int TotalPages
);

// ============================================================================
// Domain Objects
// ============================================================================

public record CfUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("suspended")] bool Suspended
);

public record CfAccount(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name
);

public record CfZone(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name_servers")] string[] NameServers,
    [property: JsonPropertyName("account")] CfAccount Account,
    [property: JsonPropertyName("plan")] CfPlan Plan
);

public record CfPlan(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("legacy_id")] string LegacyId
);

// ============================================================================
// Request Objects
// ============================================================================

public record CreateZoneRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("account")] AccountRef Account,
    [property: JsonPropertyName("type")] string Type = "full",
    [property: JsonPropertyName("jump_start")] bool JumpStart = true
);

public record AccountRef(
    [property: JsonPropertyName("id")] string Id
);

public record PurgeCacheRequest(
    [property: JsonPropertyName("purge_everything")] bool PurgeEverything = true
);

public record PurgeCacheResponse(
    [property: JsonPropertyName("id")] string Id
);

// ============================================================================
// Paginated Result (internal, not API)
// ============================================================================

public record PaginatedResult<T>(
    List<T> Items,
    PaginationInfo Pagination
);

// ============================================================================
// State Models — Orchestration
// ============================================================================

public enum OperationKind { Create, Delete, Purge }

public enum PreflightStatus { WillCreate, Exists, Invalid, Duplicate }

public record PreflightEntry(string Domain, PreflightStatus Status, string? ExistingZoneId = null);

public enum TaskStatus { Queued, Running, Success, Failed, Skipped, Blocked }

public record TaskEntry
{
    public required string Id { get; init; }
    public required string Domain { get; init; }
    public string? ZoneName { get; init; }
    public required OperationKind Operation { get; init; }
    public TaskStatus Status { get; set; }
    public int Attempt { get; set; }
    public int? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public long? LatencyMs { get; set; }
}

public enum BatchStatus { Pending, Running, Paused, Completed, Cancelled }

public class BatchState
{
    public required OperationKind Operation { get; init; }
    public required string AccountId { get; init; }
    public BatchStatus Status { get; set; }
    public int TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public long? EtaMs { get; set; }
}
