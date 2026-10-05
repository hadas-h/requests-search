using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Requests.Domain.Entities;
using Requests.Domain.Interfaces;

namespace Requests.Application.Requests;

public sealed class RequestService : IRequestService
{
    private readonly IRequestRepository _repository;

    public RequestService(IRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<RequestDto>> SearchAsync(
        RequestQuery query,
        int currentUserId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var q = _repository.Query()
            .ApplyAuthorization(currentUserId, isAdministrator)
            .ApplyFilters(query)
            .ApplySort(query)
            .ApplyKeyset(query);

        var rows = await q
            .Select(r => new RequestDto(
                r.Id,
                r.RequestNumber,
                r.CustomerId,
                r.OwnerId,
                r.AssignedToUserId,
                new EnumRef((int)r.Status, r.Status.ToString()),
                new EnumRef((int)r.RequestType, r.RequestType.ToString()),
                r.CreatedAt))
            .Take(query.PageSize + 1)
            .ToListAsync(cancellationToken);

        string? nextCursor = null;
        if (rows.Count > query.PageSize)
        {
            rows = rows.Take(query.PageSize).ToList();
            var last = rows[^1];
            nextCursor = CursorCodec.Encode(BuildCursor(query, last));
        }

        return new PagedResult<RequestDto>(rows, nextCursor);
    }

    public async Task<RequestStats> StatsAsync(
        int currentUserId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var authorized = _repository.Query().ApplyAuthorization(currentUserId, isAdministrator);

        var statusCounts = await authorized
            .GroupBy(r => r.Status)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var typeCounts = await authorized
            .GroupBy(r => r.RequestType)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Include every enum value (zero when absent) so the dashboard renders a stable set of
        // slices regardless of which statuses/types currently have data.
        var byStatus = Enum.GetValues<RequestStatus>()
            .Select(s => new EnumCount(
                new EnumRef((int)s, s.ToString()),
                statusCounts.FirstOrDefault(c => c.Value == s)?.Count ?? 0))
            .ToList();

        var byType = Enum.GetValues<RequestType>()
            .Select(t => new EnumCount(
                new EnumRef((int)t, t.ToString()),
                typeCounts.FirstOrDefault(c => c.Value == t)?.Count ?? 0))
            .ToList();

        var total = byStatus.Sum(x => x.Count);
        return new RequestStats(total, byStatus, byType);
    }

    // LastValue is formatted per sort field to match exactly how ApplyKeyset decodes it,
    // keeping keyset paging gap- and duplicate-free. Status/RequestType encode the enum name,
    // matching the alphabetical ApplySort order.
    private static Cursor BuildCursor(RequestQuery query, RequestDto last)
    {
        var lastValue = query.SortBy switch
        {
            SortField.RequestNumber => last.RequestNumber,
            SortField.Status => last.Status.Name,
            SortField.RequestType => last.RequestType.Name,
            _ => last.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
        };

        return new Cursor(query.SortBy, query.Direction, lastValue, last.Id);
    }
}
