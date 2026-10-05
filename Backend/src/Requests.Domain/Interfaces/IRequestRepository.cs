using Requests.Domain.Entities;

namespace Requests.Domain.Interfaces;

public interface IRequestRepository
{
    // Read-only, composable query surface for DB-level filtering, authorization,
    // sorting, and keyset pagination. Uses AsNoTracking() (see implementation).
    IQueryable<Request> Query();
}
