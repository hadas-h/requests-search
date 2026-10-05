using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Requests.Api.Controllers.Common;
using Requests.Application.Requests;

namespace Requests.Api.Controllers.Requests;

[Authorize]
[Route("api/requests/stats")]
public sealed class RequestStatsController : BaseController
{
    private readonly IRequestService _service;

    public RequestStatsController(IRequestService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<RequestStats>> Post(CancellationToken cancellationToken)
    {
        var stats = await _service.StatsAsync(CurrentUserId, IsAdministrator, cancellationToken);
        return Ok(stats);
    }
}
