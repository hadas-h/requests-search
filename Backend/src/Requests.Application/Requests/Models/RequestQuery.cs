using Requests.Domain.Entities;

namespace Requests.Application.Requests;

public sealed record RequestQuery
{
    public string? RequestNumber { get; init; }
    public IReadOnlyList<RequestStatus> Statuses { get; init; } = [];
    public RequestType? RequestType { get; init; }
    public DateTime? CreatedFrom { get; init; }   // UTC
    public DateTime? CreatedTo { get; init; }     // UTC
    public SortField SortBy { get; init; } = SortField.CreatedAt;
    public SortDirection Direction { get; init; } = SortDirection.Desc;
    public int PageSize { get; init; } = 50;
    public Cursor? After { get; init; }
}
