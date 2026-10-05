namespace Requests.Application.Requests;

/// <summary>
/// Aggregate counts for the caller's authorized Requests, grouped by status and by type, plus the
/// overall total. Computed in a single database-level aggregation so the dashboard needs one call.
/// </summary>
public sealed record RequestStats(
    int Total,
    IReadOnlyList<EnumCount> ByStatus,
    IReadOnlyList<EnumCount> ByType);

/// <summary>A single group bucket: the enum value (id + name) and how many Requests it holds.</summary>
public sealed record EnumCount(EnumRef Value, int Count);
