namespace Requests.Application.Requests;

/// <summary>
/// Raw, unvalidated search parameters. Every value is kept as a string (or string array) so that
/// invalid input — an unparseable date, a non-integer page size, an unknown enum name — surfaces
/// as a controlled HTTP 400 from <see cref="RequestQueryValidator"/> rather than an opaque
/// model-binding failure. The validator maps these into a validated <see cref="RequestQuery"/>.
/// </summary>
public sealed class RequestQueryParameters
{
    public string? RequestNumber { get; init; }
    public string[]? Status { get; init; }
    public string? CreatedFrom { get; init; }
    public string? CreatedTo { get; init; }
    public string? RequestType { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
    public string? PageSize { get; init; }
    public string? Cursor { get; init; }
}
