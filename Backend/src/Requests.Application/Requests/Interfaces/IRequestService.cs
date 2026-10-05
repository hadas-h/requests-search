namespace Requests.Application.Requests;

public interface IRequestService
{

    Task<PagedResult<RequestDto>> SearchAsync(
        RequestQuery query,
        int currentUserId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<RequestStats> StatsAsync(
        int currentUserId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);
}
