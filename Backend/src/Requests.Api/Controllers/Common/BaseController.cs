using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Requests.Application.Auth;

namespace Requests.Api.Controllers.Common;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController : ControllerBase
{
    protected int CurrentUserId
    {
        get
        {
            var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(sub, out var id))
            {
                throw new InvalidOperationException("The authenticated token does not contain a valid user id.");
            }

            return id;
        }
    }

    protected bool IsAdministrator =>
        string.Equals(User.FindFirstValue(ITokenService.AdminClaim), "true", StringComparison.OrdinalIgnoreCase);
}
