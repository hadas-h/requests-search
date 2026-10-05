using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Requests.Api.Controllers.Common;
using Requests.Application.Requests;

namespace Requests.Api.Controllers.Requests;

[Authorize]
[Route("api/requests/search")]
public sealed class RequestSearchController : BaseController
{
    private readonly IRequestService _service;
    private readonly RequestQueryValidator _validator;

    public RequestSearchController(IRequestService service, RequestQueryValidator validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpPost]
    public async Task<ActionResult<PagedResult<RequestDto>>> Post(
        [FromBody] RequestSearchRequest request,
        CancellationToken cancellationToken)
    {
        var (query, errors) = _validator.Parse(request ?? new RequestSearchRequest());
        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var result = await _service.SearchAsync(query, CurrentUserId, IsAdministrator, cancellationToken);
        return Ok(result);
    }
}
