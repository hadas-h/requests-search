namespace Requests.Application.Auth;

public sealed record RegisterRequest(string Username, string Password);

public sealed record LoginRequest(string Username, string Password);

public sealed record UserDto(int Id, string Username, bool IsAdministrator);

public sealed record AuthResult(string Token, UserDto User);
