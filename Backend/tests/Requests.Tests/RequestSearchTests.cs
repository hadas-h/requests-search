using Microsoft.EntityFrameworkCore;
using Requests.Application.Requests;
using Requests.Domain.Entities;
using Requests.Domain.Interfaces;
using Requests.Infrastructure.Persistence;
using Xunit;

namespace Requests.Tests;

/// <summary>
/// Behavioral tests for <see cref="RequestService.SearchAsync"/> exercised over an EF Core
/// InMemory-backed repository (the real AsNoTracking read path). These cover the high-value
/// correctness properties from the design:
/// <list type="bullet">
///   <item>Property 1 — Authorization containment (R16.1).</item>
///   <item>Property 2 — Filter conjunction (R16.2).</item>
///   <item>Property 4 — Keyset completeness and disjointness (R16.3).</item>
/// </list>
/// </summary>
public class RequestSearchTests
{
    private const int Me = 42;
    private const int Other = 99;

    // ---------------------------------------------------------------------
    // Property 1: Authorization containment (R16.1)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task RegularUser_SeesOnlyOwnedOrAssignedRequests()
    {
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: Me, assignedTo: Other),   // owned
            Create(2, ownerId: Other, assignedTo: Me),    // assigned
            Create(3, ownerId: Other, assignedTo: Other), // neither
            Create(4, ownerId: Other, assignedTo: null),  // neither
        ]);
        var service = new RequestService(repository);

        var result = await service.SearchAsync(new RequestQuery { PageSize = 100 }, Me, isAdministrator: false);

        Assert.Equal(new[] { 1, 2 }, result.Items.Select(x => x.Id).OrderBy(id => id));
        Assert.All(result.Items, dto => Assert.True(dto.OwnerId == Me || dto.AssignedToUserId == Me));
    }

    [Fact]
    public async Task Administrator_SeesAllRequests()
    {
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: Me, assignedTo: Other),
            Create(2, ownerId: Other, assignedTo: Other),
            Create(3, ownerId: Other, assignedTo: null),
        ]);
        var service = new RequestService(repository);

        var result = await service.SearchAsync(new RequestQuery { PageSize = 100 }, currentUserId: 1, isAdministrator: true);

        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task RegularUser_FilteringByStatusOnlyPresentOnOthersRequests_ReturnsNone()
    {
        // The Cancelled status exists only on requests belonging to other users.
        // A regular user must NEVER see them, even when explicitly filtering by that status.
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: Me, assignedTo: null, status: RequestStatus.New),
            Create(2, ownerId: Me, assignedTo: null, status: RequestStatus.InProgress),
            Create(3, ownerId: Other, assignedTo: Other, status: RequestStatus.Cancelled),
            Create(4, ownerId: Other, assignedTo: null, status: RequestStatus.Cancelled),
        ]);
        var service = new RequestService(repository);

        var query = new RequestQuery
        {
            Statuses = new[] { RequestStatus.Cancelled },
            PageSize = 100,
        };

        var result = await service.SearchAsync(query, Me, isAdministrator: false);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task RegularUser_FiltersNeverWidenTheAuthorizedSet()
    {
        // Regardless of parameters, a regular user's result is always a subset of their
        // authorized set. Here every non-authorization filter is "open" yet others' rows never leak.
        var requests = new List<Request>
        {
            Create(1, ownerId: Me, assignedTo: null, status: RequestStatus.New, type: RequestType.Legal),
            Create(2, ownerId: Other, assignedTo: Me, status: RequestStatus.Completed, type: RequestType.Payment),
            Create(3, ownerId: Other, assignedTo: Other, status: RequestStatus.New, type: RequestType.Legal),
            Create(4, ownerId: Other, assignedTo: null, status: RequestStatus.Completed, type: RequestType.Payment),
        };
        var repository = new FakeRequestRepository(requests);
        var service = new RequestService(repository);

        var authorizedIds = requests
            .Where(r => r.OwnerId == Me || r.AssignedToUserId == Me)
            .Select(r => r.Id)
            .ToHashSet();

        // A grab-bag of queries that could tempt leakage.
        var queries = new[]
        {
            new RequestQuery { PageSize = 100 },
            new RequestQuery { Statuses = new[] { RequestStatus.New }, PageSize = 100 },
            new RequestQuery { Statuses = new[] { RequestStatus.New, RequestStatus.Completed }, PageSize = 100 },
            new RequestQuery { RequestType = RequestType.Legal, PageSize = 100 },
            new RequestQuery { RequestNumber = "REQ", PageSize = 100 },
        };

        foreach (var query in queries)
        {
            var result = await service.SearchAsync(query, Me, isAdministrator: false);
            Assert.All(result.Items, dto => Assert.Contains(dto.Id, authorizedIds));
        }
    }

    // ---------------------------------------------------------------------
    // Property 2: Filter conjunction (R16.2)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CombinedFilters_AppliedAsConjunction_EveryRowSatisfiesAllPredicates()
    {
        var from = new DateTime(2024, 03, 01, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2024, 03, 31, 23, 59, 59, DateTimeKind.Utc);

        var repository = new FakeRequestRepository(
        [
            // Matches all predicates.
            Create(1, ownerId: Me, assignedTo: null, number: "REQ-ALPHA-001",
                status: RequestStatus.New, type: RequestType.Legal, createdAt: new DateTime(2024, 03, 10, 0, 0, 0, DateTimeKind.Utc)),
            Create(2, ownerId: Me, assignedTo: null, number: "REQ-ALPHA-002",
                status: RequestStatus.InProgress, type: RequestType.Legal, createdAt: new DateTime(2024, 03, 20, 0, 0, 0, DateTimeKind.Utc)),
            // Wrong number substring.
            Create(3, ownerId: Me, assignedTo: null, number: "REQ-BETA-003",
                status: RequestStatus.New, type: RequestType.Legal, createdAt: new DateTime(2024, 03, 12, 0, 0, 0, DateTimeKind.Utc)),
            // Wrong status.
            Create(4, ownerId: Me, assignedTo: null, number: "REQ-ALPHA-004",
                status: RequestStatus.Completed, type: RequestType.Legal, createdAt: new DateTime(2024, 03, 12, 0, 0, 0, DateTimeKind.Utc)),
            // Wrong type.
            Create(5, ownerId: Me, assignedTo: null, number: "REQ-ALPHA-005",
                status: RequestStatus.New, type: RequestType.Payment, createdAt: new DateTime(2024, 03, 12, 0, 0, 0, DateTimeKind.Utc)),
            // Out of date range (before).
            Create(6, ownerId: Me, assignedTo: null, number: "REQ-ALPHA-006",
                status: RequestStatus.New, type: RequestType.Legal, createdAt: new DateTime(2024, 02, 28, 0, 0, 0, DateTimeKind.Utc)),
            // Out of date range (after).
            Create(7, ownerId: Me, assignedTo: null, number: "REQ-ALPHA-007",
                status: RequestStatus.New, type: RequestType.Legal, createdAt: new DateTime(2024, 04, 01, 0, 0, 0, DateTimeKind.Utc)),
        ]);
        var service = new RequestService(repository);

        var statuses = new[] { RequestStatus.New, RequestStatus.InProgress };
        var query = new RequestQuery
        {
            RequestNumber = "alpha", // case-insensitive partial match
            Statuses = statuses,
            RequestType = RequestType.Legal,
            CreatedFrom = from,
            CreatedTo = to,
            PageSize = 100,
        };

        var result = await service.SearchAsync(query, Me, isAdministrator: false);

        // Only requests 1 and 2 satisfy the conjunction of all filters.
        Assert.Equal(new[] { 1, 2 }, result.Items.Select(x => x.Id).OrderBy(id => id));

        // Every returned row satisfies ALL predicates simultaneously.
        Assert.All(result.Items, dto =>
        {
            Assert.Contains("ALPHA", dto.RequestNumber);
            Assert.Contains(dto.Status.Name, statuses.Select(s => s.ToString()));
            Assert.Equal(RequestType.Legal.ToString(), dto.RequestType.Name);
            Assert.InRange(dto.CreatedAt, from, to);
        });
    }

    [Fact]
    public async Task SortByRequestType_Ascending_IsAlphabeticalByName()
    {
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: Me, assignedTo: null, type: RequestType.Payment),
            Create(2, ownerId: Me, assignedTo: null, type: RequestType.General),
            Create(3, ownerId: Me, assignedTo: null, type: RequestType.Appeal),
            Create(4, ownerId: Me, assignedTo: null, type: RequestType.Legal),
        ]);
        var service = new RequestService(repository);

        var result = await service.SearchAsync(
            new RequestQuery { SortBy = SortField.RequestType, Direction = SortDirection.Asc, PageSize = 100 },
            Me, isAdministrator: false);

        // Alphabetical by name: Appeal, General, Legal, Payment.
        Assert.Equal(
            new[] { "Appeal", "General", "Legal", "Payment" },
            result.Items.Select(i => i.RequestType.Name).ToArray());
    }

    // ---------------------------------------------------------------------
    // Property 4: Keyset completeness and disjointness (R16.3)
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(SortField.CreatedAt, SortDirection.Desc)]
    [InlineData(SortField.CreatedAt, SortDirection.Asc)]
    [InlineData(SortField.Status, SortDirection.Asc)]   // non-unique sort field -> exercises Id tie-breaker
    [InlineData(SortField.Status, SortDirection.Desc)]
    public async Task KeysetTraversal_CoversEveryRowExactlyOnce_NoDuplicatesNoGaps(
        SortField sortBy, SortDirection direction)
    {
        // 37 rows with heavily duplicated sort values (only 4 distinct statuses and a handful of
        // duplicate CreatedAt timestamps) to stress the composite seek predicate and Id tie-breaker.
        var seed = BuildSeed(count: 37, ownerId: Me);
        var repository = new FakeRequestRepository(seed);
        var service = new RequestService(repository);

        const int pageSize = 10;
        var expectedIds = seed.Select(r => r.Id).ToHashSet();

        var collectedIds = new List<int>();
        Cursor? after = null;
        string? lastNextCursor;
        var pageCount = 0;

        do
        {
            var query = new RequestQuery
            {
                SortBy = sortBy,
                Direction = direction,
                PageSize = pageSize,
                After = after,
            };

            var page = await service.SearchAsync(query, Me, isAdministrator: false);
            pageCount++;

            // Page-size bound (Property 6).
            Assert.True(page.Items.Count <= pageSize);

            collectedIds.AddRange(page.Items.Select(x => x.Id));
            lastNextCursor = page.NextCursor;

            after = lastNextCursor is null ? null : CursorCodec.Decode(lastNextCursor);
        }
        while (lastNextCursor is not null);

        // No duplicates.
        Assert.Equal(collectedIds.Count, collectedIds.Distinct().Count());

        // No gaps: the union of all pages equals the full authorized set exactly.
        Assert.Equal(expectedIds.Count, collectedIds.Count);
        Assert.Equal(expectedIds, collectedIds.ToHashSet());

        // We actually paged (sanity that the last page terminates with a null cursor).
        Assert.True(pageCount >= 4);
    }

    [Fact]
    public async Task LastPage_HasNullNextCursor_WhenResultsFitExactlyInPages()
    {
        // Exactly 20 rows over a page size of 10: two full pages, then the cursor must be null.
        var seed = BuildSeed(count: 20, ownerId: Me);
        var repository = new FakeRequestRepository(seed);
        var service = new RequestService(repository);

        const int pageSize = 10;

        var page1 = await service.SearchAsync(
            new RequestQuery { PageSize = pageSize }, Me, isAdministrator: false);
        Assert.Equal(pageSize, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);

        var page2 = await service.SearchAsync(
            new RequestQuery { PageSize = pageSize, After = CursorCodec.Decode(page1.NextCursor!) },
            Me, isAdministrator: false);
        Assert.Equal(pageSize, page2.Items.Count);

        // All 20 consumed -> no further page.
        Assert.Null(page2.NextCursor);
    }

    [Fact]
    public async Task EmptyResult_ReturnsNoItemsAndNullNextCursor()
    {
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: Other, assignedTo: Other),
        ]);
        var service = new RequestService(repository);

        var result = await service.SearchAsync(new RequestQuery { PageSize = 50 }, Me, isAdministrator: false);

        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task Search_WithUnparseableCreatedAtCursorValue_ThrowsMalformedCursor()
    {
        // Defense-in-depth: a cursor whose CreatedAt LastValue is not a valid date-time must surface
        // as a Malformed CursorException (→ HTTP 400) rather than an unhandled FormatException.
        var repository = new FakeRequestRepository([Create(1, ownerId: Me, assignedTo: null)]);
        var service = new RequestService(repository);

        var query = new RequestQuery
        {
            SortBy = SortField.CreatedAt,
            Direction = SortDirection.Desc,
            PageSize = 10,
            After = new Cursor(SortField.CreatedAt, SortDirection.Desc, "not-a-date", 1),
        };

        var ex = await Assert.ThrowsAsync<CursorException>(
            () => service.SearchAsync(query, Me, isAdministrator: false));
        Assert.Equal(CursorErrorKind.Malformed, ex.Kind);
    }

    // ---------------------------------------------------------------------
    // Dashboard stats aggregation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Stats_CountsAuthorizedRequestsByStatusAndType_WithZeroBucketsAndTotal()
    {
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: Me, assignedTo: null, status: RequestStatus.New, type: RequestType.Legal),
            Create(2, ownerId: Me, assignedTo: null, status: RequestStatus.New, type: RequestType.General),
            Create(3, ownerId: Other, assignedTo: Me, status: RequestStatus.InProgress, type: RequestType.Legal),
            // Not authorized for Me -> must be excluded from the counts.
            Create(4, ownerId: Other, assignedTo: Other, status: RequestStatus.Completed, type: RequestType.Payment),
        ]);
        var service = new RequestService(repository);

        var stats = await service.StatsAsync(Me, isAdministrator: false);

        Assert.Equal(3, stats.Total);

        // Every status appears (zero when absent).
        Assert.Equal(4, stats.ByStatus.Count);
        Assert.Equal(2, StatusCount(stats, RequestStatus.New));
        Assert.Equal(1, StatusCount(stats, RequestStatus.InProgress));
        Assert.Equal(0, StatusCount(stats, RequestStatus.Completed));
        Assert.Equal(0, StatusCount(stats, RequestStatus.Cancelled));

        // Every type appears (zero when absent).
        Assert.Equal(4, stats.ByType.Count);
        Assert.Equal(2, TypeCount(stats, RequestType.Legal));
        Assert.Equal(1, TypeCount(stats, RequestType.General));
        Assert.Equal(0, TypeCount(stats, RequestType.Payment));
        Assert.Equal(0, TypeCount(stats, RequestType.Appeal));
    }

    [Fact]
    public async Task Stats_ForAdministrator_CountsAllRequests()
    {
        var repository = new FakeRequestRepository(
        [
            Create(1, ownerId: Me, assignedTo: null, status: RequestStatus.New),
            Create(2, ownerId: Other, assignedTo: Other, status: RequestStatus.Completed),
        ]);
        var service = new RequestService(repository);

        var stats = await service.StatsAsync(Me, isAdministrator: true);

        Assert.Equal(2, stats.Total);
    }

    private static int StatusCount(RequestStats stats, RequestStatus status) =>
        stats.ByStatus.First(c => c.Value.Id == (int)status).Count;

    private static int TypeCount(RequestStats stats, RequestType type) =>
        stats.ByType.First(c => c.Value.Id == (int)type).Count;

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static List<Request> BuildSeed(int count, int ownerId)
    {
        var statuses = Enum.GetValues<RequestStatus>();
        var types = Enum.GetValues<RequestType>();
        // A small set of distinct timestamps so many rows share the same CreatedAt,
        // forcing the Id tie-breaker to do real work.
        var baseTime = new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc);

        var seed = new List<Request>(count);
        for (var i = 1; i <= count; i++)
        {
            seed.Add(new Request
            {
                Id = i,
                RequestNumber = $"REQ-{i:0000}",
                CustomerId = i,
                OwnerId = ownerId,
                AssignedToUserId = null,
                Status = statuses[i % statuses.Length],
                RequestType = types[i % types.Length],
                // Only 5 distinct timestamps across all rows.
                CreatedAt = baseTime.AddDays(i % 5),
                UpdatedAt = baseTime.AddDays(i % 5),
            });
        }

        return seed;
    }

    private static Request Create(
        int id,
        int ownerId,
        int? assignedTo,
        string? number = null,
        RequestStatus status = RequestStatus.New,
        RequestType type = RequestType.General,
        DateTime? createdAt = null)
        => new()
        {
            Id = id,
            RequestNumber = number ?? $"REQ-{id:000}",
            CustomerId = id,
            OwnerId = ownerId,
            AssignedToUserId = assignedTo,
            Status = status,
            RequestType = type,
            CreatedAt = createdAt ?? new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc).AddMinutes(id),
            UpdatedAt = createdAt ?? new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc).AddMinutes(id),
        };

    /// <summary>
    /// Backs <see cref="IRequestRepository.Query"/> with an EF Core InMemory context (unique DB
    /// name per instance) so the async query operators used by <see cref="RequestService"/>
    /// execute against a real <c>IAsyncQueryProvider</c>, mirroring the Infrastructure repository.
    /// </summary>
    private sealed class FakeRequestRepository : IRequestRepository
    {
        private readonly RequestsDbContext _db;

        public FakeRequestRepository(List<Request> requests)
        {
            var options = new DbContextOptionsBuilder<RequestsDbContext>()
                .UseInMemoryDatabase($"SearchTests-{Guid.NewGuid():N}")
                .Options;

            _db = new RequestsDbContext(options);
            _db.Requests.AddRange(requests);
            _db.SaveChanges();
        }

        public IQueryable<Request> Query() => _db.Requests.AsNoTracking();
    }
}
