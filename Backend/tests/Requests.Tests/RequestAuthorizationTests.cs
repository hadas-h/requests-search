using Microsoft.EntityFrameworkCore;
using Requests.Application.Requests;
using Requests.Domain.Entities;
using Requests.Domain.Interfaces;
using Requests.Infrastructure.Persistence;
using Xunit;

namespace Requests.Tests;

/// <summary>
/// Focused unit coverage for the most security-critical requirement: server-side authorization.
/// A regular user must only ever see requests they own (<see cref="Request.OwnerId"/>) or are
/// assigned (<see cref="Request.AssignedToUserId"/>); an administrator sees everything. These tests
/// exercise <see cref="RequestQueryBuilder.ApplyAuthorization"/> through the service so the scope
/// cannot be widened by filters, sorting, or paging.
/// </summary>
public class RequestAuthorizationTests
{
    private const int CurrentUser = 10;
    private const int OtherUser = 99;

    [Fact]
    public async Task RegularUser_SeesRequestTheyOwn()
    {
        var service = ServiceWith(Create(1, ownerId: CurrentUser, assignedTo: OtherUser));

        var result = await service.SearchAsync(new RequestQuery(), CurrentUser, isAdministrator: false);

        Assert.Equal(new[] { 1 }, result.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task RegularUser_SeesRequestAssignedToThem()
    {
        var service = ServiceWith(Create(1, ownerId: OtherUser, assignedTo: CurrentUser));

        var result = await service.SearchAsync(new RequestQuery(), CurrentUser, isAdministrator: false);

        Assert.Equal(new[] { 1 }, result.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task RegularUser_DoesNotSeeRequestOfAnotherUser()
    {
        var service = ServiceWith(Create(1, ownerId: OtherUser, assignedTo: OtherUser));

        var result = await service.SearchAsync(new RequestQuery(), CurrentUser, isAdministrator: false);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task RegularUser_SeesOnlyOwnedOrAssigned_AcrossAMixedSet()
    {
        var service = ServiceWith(
            Create(1, ownerId: CurrentUser, assignedTo: null),      // owned
            Create(2, ownerId: OtherUser, assignedTo: CurrentUser), // assigned
            Create(3, ownerId: OtherUser, assignedTo: OtherUser),   // neither -> hidden
            Create(4, ownerId: OtherUser, assignedTo: null));       // neither -> hidden

        var result = await service.SearchAsync(new RequestQuery(), CurrentUser, isAdministrator: false);

        Assert.Equal(new[] { 1, 2 }, result.Items.Select(x => x.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task Administrator_SeesEveryRequest_IncludingUnassigned()
    {
        var service = ServiceWith(
            Create(1, ownerId: CurrentUser, assignedTo: OtherUser),
            Create(2, ownerId: OtherUser, assignedTo: null),
            Create(3, ownerId: OtherUser, assignedTo: OtherUser));

        var result = await service.SearchAsync(new RequestQuery(), CurrentUser, isAdministrator: true);

        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task Authorization_IsEnforcedBeforeFilters_SoAFilterCannotLeakOthersRows()
    {
        // Both requests have Status = New. A regular user filtering by New must still only get
        // their own row — the authorization scope is applied before the status filter.
        var service = ServiceWith(
            Create(1, ownerId: CurrentUser, assignedTo: null, status: RequestStatus.New),
            Create(2, ownerId: OtherUser, assignedTo: OtherUser, status: RequestStatus.New));

        var query = new RequestQuery { Statuses = [RequestStatus.New] };

        var result = await service.SearchAsync(query, CurrentUser, isAdministrator: false);

        Assert.Equal(new[] { 1 }, result.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task RegularUser_WithNoOwnedOrAssignedRequests_SeesEmptyResult()
    {
        var service = ServiceWith(
            Create(1, ownerId: OtherUser, assignedTo: OtherUser),
            Create(2, ownerId: OtherUser, assignedTo: null));

        var result = await service.SearchAsync(new RequestQuery(), CurrentUser, isAdministrator: false);

        Assert.Empty(result.Items);
    }

    private static RequestService ServiceWith(params Request[] requests)
        => new(new FakeRequestRepository(requests));

    private static Request Create(
        int id, int ownerId, int? assignedTo, RequestStatus status = RequestStatus.New)
        => new()
        {
            Id = id,
            RequestNumber = $"REQ-{id:000}",
            CustomerId = id,
            OwnerId = ownerId,
            AssignedToUserId = assignedTo,
            Status = status,
            RequestType = RequestType.General,
            CreatedAt = DateTime.UtcNow.AddMinutes(-id)
        };

    // Backs Query() with an EF Core InMemory context so async operators (ToListAsync) run against a
    // real IAsyncQueryProvider, mirroring the Infrastructure repository.
    private sealed class FakeRequestRepository : IRequestRepository
    {
        private readonly RequestsDbContext _db;

        public FakeRequestRepository(IEnumerable<Request> requests)
        {
            var options = new DbContextOptionsBuilder<RequestsDbContext>()
                .UseInMemoryDatabase($"AuthzRequests-{Guid.NewGuid():N}")
                .Options;

            _db = new RequestsDbContext(options);
            _db.Requests.AddRange(requests);
            _db.SaveChanges();
        }

        public IQueryable<Request> Query() => _db.Requests.AsNoTracking();
    }
}
