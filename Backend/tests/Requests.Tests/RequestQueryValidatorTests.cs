using Requests.Application.Requests;
using Requests.Domain.Entities;
using Xunit;

namespace Requests.Tests;

/// <summary>
/// Tests for <see cref="RequestQueryValidator.Parse"/> — the invalid-input path (R16.5 / R8).
/// The controller returns <c>ValidationProblem</c> (HTTP 400) whenever the returned errors
/// dictionary is non-empty, so verifying the collected errors here validates the 400 behavior.
/// The key property: when several parameters are invalid, ALL of them are reported together,
/// not just the first.
/// </summary>
public class RequestQueryValidatorTests
{
    private readonly RequestQueryValidator _validator = new();

    [Fact]
    public void Parse_ValidParameters_ProducesNoErrors()
    {
        var raw = new RequestQueryParameters
        {
            RequestNumber = "REQ",
            Status = new[] { "New", "InProgress" },
            RequestType = "Legal",
            CreatedFrom = "2024-01-01T00:00:00Z",
            CreatedTo = "2024-12-31T00:00:00Z",
            SortBy = "CreatedAt",
            SortDirection = "Desc",
            PageSize = "50",
        };

        var (query, errors) = _validator.Parse(raw);

        Assert.Empty(errors);
        Assert.Equal(RequestType.Legal, query.RequestType);
        Assert.Equal(new[] { RequestStatus.New, RequestStatus.InProgress }, query.Statuses);
    }

    [Fact]
    public void Parse_MultipleInvalidParameters_ReportsAllOfThemTogether()
    {
        // Every one of these parameters is invalid simultaneously:
        //  - status: unknown enum name
        //  - requestType: unknown enum name
        //  - sortBy: unsupported sort field
        //  - pageSize: out of the 1..200 range
        //  - createdFrom/createdTo: inverted range
        var raw = new RequestQueryParameters
        {
            Status = new[] { "Frozen" },
            RequestType = "Banana",
            SortBy = "Nonsense",
            PageSize = "9999",
            CreatedFrom = "2024-12-31T00:00:00Z",
            CreatedTo = "2024-01-01T00:00:00Z",
        };

        var (_, errors) = _validator.Parse(raw);

        // ALL invalid parameters must be reported together in a single response.
        Assert.Contains("status", errors.Keys);
        Assert.Contains("requestType", errors.Keys);
        Assert.Contains("sortBy", errors.Keys);
        Assert.Contains("pageSize", errors.Keys);
        // Inverted range surfaces on both bounds.
        Assert.Contains("createdFrom", errors.Keys);
        Assert.Contains("createdTo", errors.Keys);

        // A single-error implementation would have short-circuited; assert breadth explicitly.
        Assert.True(errors.Count >= 5,
            $"Expected all invalid parameters to be reported together; got: {string.Join(", ", errors.Keys)}");
    }

    [Fact]
    public void Parse_InvalidStatus_ReportsInvalidValueWithAllowedList()
    {
        var raw = new RequestQueryParameters { Status = new[] { "Foo" } };

        var (_, errors) = _validator.Parse(raw);

        var message = Assert.Single(errors["status"]);
        Assert.Contains("Foo", message);
        Assert.Contains("New", message); // allowed-values list is included
    }

    [Fact]
    public void Parse_InvalidRequestType_ReportsError()
    {
        var raw = new RequestQueryParameters { RequestType = "NotAType" };

        var (_, errors) = _validator.Parse(raw);

        Assert.Contains("requestType", errors.Keys);
    }

    [Fact]
    public void Parse_InvalidSortDirection_ReportsError()
    {
        var raw = new RequestQueryParameters { SortDirection = "Sideways" };

        var (_, errors) = _validator.Parse(raw);

        Assert.Contains("sortDirection", errors.Keys);
    }

    [Theory]
    [InlineData("0")]     // below minimum
    [InlineData("201")]   // above maximum
    [InlineData("abc")]   // not an integer
    public void Parse_OutOfRangeOrNonIntegerPageSize_ReportsError(string pageSize)
    {
        var raw = new RequestQueryParameters { PageSize = pageSize };

        var (_, errors) = _validator.Parse(raw);

        Assert.Contains("pageSize", errors.Keys);
    }

    [Fact]
    public void Parse_UnparseableDate_ReportsError()
    {
        var raw = new RequestQueryParameters { CreatedFrom = "not-a-date" };

        var (_, errors) = _validator.Parse(raw);

        Assert.Contains("createdFrom", errors.Keys);
    }

    [Fact]
    public void Parse_InvertedDateRange_ReportsBothBounds()
    {
        var raw = new RequestQueryParameters
        {
            CreatedFrom = "2024-06-01T00:00:00Z",
            CreatedTo = "2024-01-01T00:00:00Z",
        };

        var (_, errors) = _validator.Parse(raw);

        Assert.Contains("createdFrom", errors.Keys);
        Assert.Contains("createdTo", errors.Keys);
    }

    [Fact]
    public void Parse_MalformedCursor_ReportsError()
    {
        var raw = new RequestQueryParameters { Cursor = "!!!not-a-valid-cursor!!!" };

        var (_, errors) = _validator.Parse(raw);

        Assert.Contains("cursor", errors.Keys);
    }

    [Fact]
    public void Parse_SortMismatchedCursor_ReportsError()
    {
        // Cursor encoded for CreatedAt/Desc, but the request asks for RequestNumber/Asc.
        var token = CursorCodec.Encode(
            new Cursor(SortField.CreatedAt, SortDirection.Desc, "2024-05-01T10:00:00Z", 12));

        var raw = new RequestQueryParameters
        {
            Cursor = token,
            SortBy = "RequestNumber",
            SortDirection = "Asc",
        };

        var (_, errors) = _validator.Parse(raw);

        Assert.Contains("cursor", errors.Keys);
    }
}
