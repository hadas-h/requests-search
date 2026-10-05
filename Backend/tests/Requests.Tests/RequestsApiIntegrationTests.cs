using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Requests.Application.Requests;
using Requests.Domain.Entities;
using Xunit;

namespace Requests.Tests;

/// <summary>
/// End-to-end integration tests that boot the real ASP.NET Core host (controllers, JWT auth,
/// validation, EF Core InMemory repository, and the startup seeders) via
/// <see cref="WebApplicationFactory{TEntryPoint}"/> and exercise <c>POST /api/requests/search</c>
/// over HTTP with a bearer token. These cover the high-value acceptance criteria:
/// authorization scoping, combined-filter conjunction, keyset paging, and invalid-input handling.
/// </summary>
public class RequestsApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public RequestsApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RequestingWithoutAToken_IsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/requests/search", new RequestSearchRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegularUser_OnlySeesOwnedOrAssignedRequests_AndAdminSeesMore()
    {
        // A freshly registered user is a regular (non-admin) user with a server-assigned id.
        var (regularClient, regularUserId) = await RegisterRegularUserAsync();

        var regularItems = await CollectAllAsync(regularClient, pageSize: 200);
        // Containment: every returned item is owned by or assigned to the caller — never leaked.
        Assert.All(regularItems, item =>
            Assert.True(item.OwnerId == regularUserId || item.AssignedToUserId == regularUserId,
                $"Request {item.Id} (owner={item.OwnerId}, assignee={item.AssignedToUserId}) leaked to user {regularUserId}."));

        // The seeded administrator sees the whole seeded set.
        var adminClient = await LoginAdminAsync();
        var adminItems = await CollectAllAsync(adminClient, pageSize: 200);
        Assert.Equal(500, adminItems.Count);

        // The admin reaches at least as many as (in practice more than) the regular user.
        Assert.True(adminItems.Count >= regularItems.Count,
            "Administrator should reach at least as many Requests as a regular user.");
    }

    [Fact]
    public async Task CombinedFilters_ReturnOnlyItemsSatisfyingEveryPredicate()
    {
        var client = await LoginAdminAsync();

        var createdFrom = new DateTime(2000, 01, 01, 0, 0, 0, DateTimeKind.Utc)
            .ToString("yyyy-MM-ddTHH:mm:ssZ");

        var request = new RequestSearchRequest
        {
            Status = ["New"],
            RequestType = "General",
            CreatedFrom = createdFrom,
            RequestNumber = "REQ",
            PageSize = 200,
        };
        var result = await SearchPageAsync(client, request);

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item =>
        {
            Assert.Equal(RequestStatus.New.ToString(), item.Status.Name);
            Assert.Equal((int)RequestStatus.New, item.Status.Id);
            Assert.Equal(RequestType.General.ToString(), item.RequestType.Name);
            Assert.Contains("REQ", item.RequestNumber);
            Assert.True(item.CreatedAt >= new DateTime(2000, 01, 01, 0, 0, 0, DateTimeKind.Utc));
        });
    }

    [Fact]
    public async Task KeysetPaging_TraversesAllPages_NoDuplicates_LastCursorNull()
    {
        var client = await LoginAdminAsync();
        const int pageSize = 25;

        var seenIds = new HashSet<int>();
        var pageCount = 0;
        string? cursor = null;
        string? lastCursor;

        do
        {
            var request = new RequestSearchRequest { PageSize = pageSize, Cursor = cursor };
            var page = await SearchPageAsync(client, request);
            pageCount++;

            Assert.True(page.Items.Count <= pageSize);

            foreach (var item in page.Items)
            {
                Assert.True(seenIds.Add(item.Id), $"Request {item.Id} appeared on more than one page.");
            }

            lastCursor = page.NextCursor;
            cursor = lastCursor;
        }
        while (lastCursor is not null);

        Assert.Equal(500, seenIds.Count);
        Assert.True(pageCount > 1, "Expected more than one page.");
        Assert.Null(lastCursor);
    }

    [Fact]
    public async Task InvalidParameters_ReturnBadRequest_ReportingAllErrors()
    {
        var client = await LoginAdminAsync();

        var request = new RequestSearchRequest { Status = ["Bogus"], SortBy = "Nope", PageSize = 0 };
        var response = await client.PostAsJsonAsync("/api/requests/search", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(JsonOptions);
        Assert.NotNull(problem);
        Assert.NotNull(problem!.Errors);
        Assert.True(problem.Errors!.Count >= 2,
            $"Expected multiple validation errors, got {problem.Errors!.Count}.");
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private async Task<List<RequestDto>> CollectAllAsync(HttpClient client, int pageSize)
    {
        var all = new List<RequestDto>();
        string? cursor = null;
        string? lastCursor;

        do
        {
            var request = new RequestSearchRequest { PageSize = pageSize, Cursor = cursor };
            var page = await SearchPageAsync(client, request);
            all.AddRange(page.Items);
            lastCursor = page.NextCursor;
            cursor = lastCursor;
        }
        while (lastCursor is not null);

        return all;
    }

    private async Task<PagedResult<RequestDto>> SearchPageAsync(HttpClient client, RequestSearchRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/requests/search", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedResult<RequestDto>>(JsonOptions);
        Assert.NotNull(body);
        return body!;
    }

    /// <summary>Registers a unique regular user and returns an authenticated client plus its id.</summary>
    private async Task<(HttpClient Client, int UserId)> RegisterRegularUserAsync()
    {
        var client = _factory.CreateClient();
        var username = $"user-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { username, password = "Passw0rd!" });
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.False(auth!.User.IsAdministrator);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth.User.Id);
    }

    /// <summary>Logs in as the seeded administrator and returns an authenticated client.</summary>
    private async Task<HttpClient> LoginAdminAsync()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "admin" });
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.True(auth!.User.IsAdministrator);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private sealed class ValidationProblemBody
    {
        public Dictionary<string, string[]>? Errors { get; set; }
    }

    private sealed record AuthResponse(string Token, AuthUser User);
    private sealed record AuthUser(int Id, string Username, bool IsAdministrator);
}
