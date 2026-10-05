namespace Requests.Application.Requests;

/// <summary>
/// An opaque forward-paging position for keyset pagination. Encodes the active sort field and
/// direction plus the sort value and id of the last row returned, so the next page can be fetched
/// with a seek predicate rather than an <c>OFFSET</c>. Serialized to a Base64url token by
/// <see cref="CursorCodec"/>.
/// </summary>
public sealed record Cursor(SortField SortBy, SortDirection Direction, string LastValue, int LastId);

/// <summary>
/// Distinguishes the two cursor failure modes so the validator can report them distinctly: a
/// malformed/undecodable cursor (Requirement 7.9) versus a cursor whose encoded sort does not match
/// the current request's sort (Requirement 7.10).
/// </summary>
public enum CursorErrorKind
{
    /// <summary>The cursor value could not be decoded (bad Base64url or invalid payload).</summary>
    Malformed,

    /// <summary>The cursor decoded successfully but its sort field/direction differ from the active sort.</summary>
    SortMismatch
}

/// <summary>
/// Raised when a cursor cannot be decoded (<see cref="CursorErrorKind.Malformed"/>) or when a
/// decoded cursor's sort field/direction do not match the active request sort
/// (<see cref="CursorErrorKind.SortMismatch"/>). The <see cref="Kind"/> lets the validator map each
/// failure mode to a distinct HTTP 400 message (Requirements 7.9 and 7.10).
/// </summary>
public sealed class CursorException : Exception
{
    public CursorException(CursorErrorKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public CursorException(CursorErrorKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>Identifies which cursor failure occurred.</summary>
    public CursorErrorKind Kind { get; }
}
