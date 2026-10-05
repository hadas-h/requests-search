using Microsoft.EntityFrameworkCore;
using Requests.Application.Requests;
using Requests.Domain.Entities;
using Requests.Domain.Interfaces;
using Requests.Infrastructure.Persistence;
using Xunit;

namespace Requests.Tests;

public class RequestServiceTests
{
    [Fact]
    public async Task Administrator_CanSeeAllRequests()
    {
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: 1, assignedTo: 2),
            Create(2, ownerId: 3, assignedTo: 4)
        ]);

        var service = new RequestService(repository);

        var result = await service.SearchAsync(new RequestQuery(), 1, true);

        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task RegularUser_CanSeeOwnedOrAssignedRequests()
    {
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: 1, assignedTo: 5),
            Create(2, ownerId: 3, assignedTo: 1),
            Create(3, ownerId: 3, assignedTo: 5)
        ]);

        var service = new RequestService(repository);

        var result = await service.SearchAsync(new RequestQuery(), 1, false);

        Assert.Equal(2, result.Items.Count);
        Assert.DoesNotContain(result.Items, x => x.Id == 3);
    }

    private static Request Create(int id, int ownerId, int assignedTo)
        => new()
        {
            Id = id,
            RequestNumber = $"REQ-{id:000}",
            CustomerId = id,
            OwnerId = ownerId,
            AssignedToUserId = assignedTo,
            Status = RequestStatus.New,
            RequestType = RequestType.General,
            CreatedAt = DateTime.UtcNow
        };

    // Backs Query() with an EF Core InMemory context so the async query operators
    // (ToListAsync) used by RequestService.SearchAsync execute against a real
    // IAsyncQueryProvider, mirroring the Infrastructure repository.
    private sealed class FakeRequestRepository : IRequestRepository
    {
        private readonly RequestsDbContext _db;

        public FakeRequestRepository(List<Request> requests)
        {
            var options = new DbContextOptionsBuilder<RequestsDbContext>()
                .UseInMemoryDatabase($"FakeRequests-{Guid.NewGuid():N}")
                .Options;

            _db = new RequestsDbContext(options);
            _db.Requests.AddRange(requests);
            _db.SaveChanges();
        }

        public IQueryable<Request> Query() => _db.Requests.AsNoTracking();
    }
}
