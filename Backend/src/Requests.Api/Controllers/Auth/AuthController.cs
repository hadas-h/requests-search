using Microsoft.AspNetCore.Mvc;
using Requests.Application.Auth;

namespace Requests.Api.Controllers.Auth;

/// <summary>
/// Registration and login. On success returns a signed JWT the client sends as
/// <c>Authorization: Bearer</c> on subsequent requests.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResult>> Register(
        [FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _auth.RegisterAsync(request, cancellationToken));
        }
        catch (AuthException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResult>> Login(
        [FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _auth.LoginAsync(request, cancellationToken));
        }
        catch (AuthException)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }
    }
}
