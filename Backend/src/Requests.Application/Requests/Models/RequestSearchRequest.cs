namespace Requests.Application.Requests;

/// <summary>
/// Request body for <c>POST /api/requests/search</c>. Criteria are sent in the body (not the URL).
/// All fields are optional; an empty body returns the first page. String/loosely-typed fields let
/// invalid input surface as a controlled HTTP 400 from <see cref="RequestQueryValidator"/>.
/// </summary>
public sealed class RequestSearchRequest
{
    public string? RequestNumber { get; init; }

    public string[]? Status { get; init; }

    public string? CreatedFrom { get; init; }

    public string? CreatedTo { get; init; }

    public string? RequestType { get; init; }

    public string? SortBy { get; init; }

    public string? SortDirection { get; init; }

    public int? PageSize { get; init; }

    public string? Cursor { get; init; }
}
