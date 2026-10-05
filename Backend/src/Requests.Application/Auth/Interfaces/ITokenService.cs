using Requests.Domain.Entities;

namespace Requests.Application.Auth;

public interface ITokenService
{

    const string AdminClaim = "is_admin";

    string CreateToken(User user);
}
