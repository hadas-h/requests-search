namespace Requests.Application.Requests;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, string? NextCursor);
