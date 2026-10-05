using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Requests.Domain.Entities;

namespace Requests.Application.Requests;

/// <summary>
/// Composable <see cref="IQueryable{Request}"/> extensions for authorization, filtering, sorting,
/// and keyset pagination. Each step stays translatable to a single data-store query.
/// </summary>
public static class RequestQueryBuilder
{
    public static IQueryable<Request> ApplyAuthorization(
        this IQueryable<Request> q, int userId, bool isAdmin)
        => isAdmin ? q : q.Where(r => r.OwnerId == userId || r.AssignedToUserId == userId);

    public static IQueryable<Request> ApplyFilters(this IQueryable<Request> q, RequestQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.RequestNumber))
        {
            // Like with %term% gives a case-insensitive, collation-independent partial match
            // that translates to a SQL LIKE and evaluates consistently on the InMemory provider.
            var term = query.RequestNumber.Trim();
            q = q.Where(r => EF.Functions.Like(r.RequestNumber, $"%{term}%"));
        }
        if (query.Statuses.Count > 0)
            q = q.Where(r => query.Statuses.Contains(r.Status));
        if (query.RequestType is { } type)
            q = q.Where(r => r.RequestType == type);
        if (query.CreatedFrom is { } from)
            q = q.Where(r => r.CreatedAt >= from);
        if (query.CreatedTo is { } to)
            q = q.Where(r => r.CreatedAt <= to);
        return q;
    }

    /// <summary>
    /// Orders by the requested field with <see cref="Request.Id"/> as the tie-breaker, giving a
    /// stable total order (required for correct keyset paging). Defaults to CreatedAt descending.
    /// </summary>
    public static IOrderedQueryable<Request> ApplySort(this IQueryable<Request> q, RequestQuery query)
    {
        bool asc = query.Direction == SortDirection.Asc;
        return query.SortBy switch
        {
            SortField.RequestNumber => asc
                ? q.OrderBy(r => r.RequestNumber).ThenBy(r => r.Id)
                : q.OrderByDescending(r => r.RequestNumber).ThenByDescending(r => r.Id),
            // Status/RequestType sort alphabetically by their (English) name, which is what the UI
            // shows (e.g. Cancelled, Completed, InProgress, New). A new enum value sorts into its
            // correct alphabetical place automatically — no mapping. Tradeoff: ordering by the name
            // string is not sargable on a relational provider, so sorting by these two fields does
            // not use the (Status)/(RequestType) index. This is a deliberate UX-over-index choice;
            // the default CreatedAt sort (the common case) remains fully index-backed.
            SortField.Status => asc
                ? q.OrderBy(r => r.Status.ToString()).ThenBy(r => r.Id)
                : q.OrderByDescending(r => r.Status.ToString()).ThenByDescending(r => r.Id),
            SortField.RequestType => asc
                ? q.OrderBy(r => r.RequestType.ToString()).ThenBy(r => r.Id)
                : q.OrderByDescending(r => r.RequestType.ToString()).ThenByDescending(r => r.Id),
            _ => asc
                ? q.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                : q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id),
        };
    }

    /// <summary>
    /// Applies the keyset seek predicate for the page after the cursor, or returns the query
    /// unchanged for the first page. The predicate mirrors <see cref="ApplySort"/> so pages
    /// never skip or duplicate a row:
    /// ascending  => (Field &gt; last) OR (Field == last AND Id &gt; lastId);
    /// descending => the same with &lt;.
    /// The cursor's LastValue is formatted per sort field to match how it is decoded here.
    /// </summary>
    public static IQueryable<Request> ApplyKeyset(this IQueryable<Request> q, RequestQuery query)
    {
        if (query.After is not { } cursor)
            return q;

        bool asc = query.Direction == SortDirection.Asc;
        int lastId = cursor.LastId;

        return query.SortBy switch
        {
            SortField.RequestNumber => ApplyRequestNumberSeek(q, cursor.LastValue, lastId, asc),
            SortField.Status => ApplyStatusSeek(q, cursor.LastValue, lastId, asc),
            SortField.RequestType => ApplyRequestTypeSeek(q, cursor.LastValue, lastId, asc),
            _ => ApplyCreatedAtSeek(q, cursor.LastValue, lastId, asc),
        };
    }

    private static IQueryable<Request> ApplyCreatedAtSeek(
        IQueryable<Request> q, string lastValue, int lastId, bool asc)
    {
        // The cursor normally arrives already-decoded via the validator, but guard the parse here
        // too so a bad LastValue surfaces as a Malformed cursor (mapped to HTTP 400) instead of an
        // unhandled FormatException — keeping behavior consistent with CursorCodec's own decoding.
        if (!DateTime.TryParse(
                lastValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var last))
        {
            throw new CursorException(
                CursorErrorKind.Malformed, "The cursor value is invalid and could not be decoded.");
        }

        return asc
            ? q.Where(r => r.CreatedAt > last || (r.CreatedAt == last && r.Id > lastId))
            : q.Where(r => r.CreatedAt < last || (r.CreatedAt == last && r.Id < lastId));
    }

    private static IQueryable<Request> ApplyRequestNumberSeek(
        IQueryable<Request> q, string lastValue, int lastId, bool asc)
        => asc
            ? q.Where(r => string.Compare(r.RequestNumber, lastValue, StringComparison.Ordinal) > 0
                || (r.RequestNumber == lastValue && r.Id > lastId))
            : q.Where(r => string.Compare(r.RequestNumber, lastValue, StringComparison.Ordinal) < 0
                || (r.RequestNumber == lastValue && r.Id < lastId));

    // Seeks by the enum name string so paging matches the ApplySort (alphabetical) order exactly.
    private static IQueryable<Request> ApplyStatusSeek(
        IQueryable<Request> q, string lastValue, int lastId, bool asc)
        => asc
            ? q.Where(r => string.Compare(r.Status.ToString(), lastValue, StringComparison.Ordinal) > 0
                || (r.Status.ToString() == lastValue && r.Id > lastId))
            : q.Where(r => string.Compare(r.Status.ToString(), lastValue, StringComparison.Ordinal) < 0
                || (r.Status.ToString() == lastValue && r.Id < lastId));

    private static IQueryable<Request> ApplyRequestTypeSeek(
        IQueryable<Request> q, string lastValue, int lastId, bool asc)
        => asc
            ? q.Where(r => string.Compare(r.RequestType.ToString(), lastValue, StringComparison.Ordinal) > 0
                || (r.RequestType.ToString() == lastValue && r.Id > lastId))
            : q.Where(r => string.Compare(r.RequestType.ToString(), lastValue, StringComparison.Ordinal) < 0
                || (r.RequestType.ToString() == lastValue && r.Id < lastId));
}
