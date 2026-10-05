using Microsoft.EntityFrameworkCore;
using Requests.Application.Requests;
using Requests.Domain.Entities;
using Requests.Domain.Interfaces;
using Requests.Infrastructure.Persistence;

namespace Requests.Infrastructure.Repositories;

public sealed class RequestRepository : IRequestRepository
{
    private readonly RequestsDbContext _db;

    public RequestRepository(RequestsDbContext db)
    {
        _db = db;
    }

    // Read-only search path: no tracking overhead. Returns IQueryable so that
    // filtering, authorization, sorting, and pagination compose at the DB level.
    public IQueryable<Request> Query()
    {
        return _db.Requests.AsNoTracking();
    }
}
