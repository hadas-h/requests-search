using System.Globalization;
using Requests.Domain.Entities;

namespace Requests.Application.Requests;

/// <summary>
/// Parses raw search parameters into a validated <see cref="RequestQuery"/>, collecting every
/// error (keyed by parameter name) rather than stopping at the first, so the controller can return
/// them together as a single HTTP 400.
/// </summary>
public sealed class RequestQueryValidator
{
    private const int MinPageSize = 1;
    private const int MaxPageSize = 200;
    private const int DefaultPageSize = 50;

    private static readonly string AllowedStatuses = string.Join(", ", Enum.GetNames<RequestStatus>());
    private static readonly string AllowedRequestTypes = string.Join(", ", Enum.GetNames<RequestType>());
    private static readonly string AllowedSortFields = string.Join(", ", Enum.GetNames<SortField>());
    private static readonly string AllowedSortDirections = string.Join(", ", Enum.GetNames<SortDirection>());

    /// <summary>Validates the search request body, reusing the shared parameter-level rules.</summary>
    public (RequestQuery Query, IDictionary<string, string[]> Errors) Parse(RequestSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Parse(new RequestQueryParameters
        {
            RequestNumber = request.RequestNumber,
            Status = request.Status,
            CreatedFrom = request.CreatedFrom,
            CreatedTo = request.CreatedTo,
            RequestType = request.RequestType,
            SortBy = request.SortBy,
            SortDirection = request.SortDirection,
            PageSize = request.PageSize?.ToString(CultureInfo.InvariantCulture),
            Cursor = request.Cursor
        });
    }

    public (RequestQuery Query, IDictionary<string, string[]> Errors) Parse(RequestQueryParameters raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var errors = new Dictionary<string, List<string>>();

        var requestNumber = string.IsNullOrWhiteSpace(raw.RequestNumber) ? null : raw.RequestNumber.Trim();
        var statuses = ParseStatuses(raw.Status, errors);
        var requestType = ParseRequestType(raw.RequestType, errors);
        var createdFrom = ParseDate(raw.CreatedFrom, "createdFrom", errors);
        var createdTo = ParseDate(raw.CreatedTo, "createdTo", errors);

        if (createdFrom is { } from && createdTo is { } to && from > to)
        {
            AddError(errors, "createdFrom", "createdFrom must be earlier than or equal to createdTo.");
            AddError(errors, "createdTo", "createdTo must be later than or equal to createdFrom.");
        }

        var sortBy = ParseSortField(raw.SortBy, errors);
        var direction = ParseSortDirection(raw.SortDirection, errors);
        var pageSize = ParsePageSize(raw.PageSize, errors);

        // Resolve sort first (with defaults) so the cursor is decoded against the expected sort
        // even when sortBy/sortDirection themselves were invalid.
        var cursor = ParseCursor(raw.Cursor, sortBy, direction, errors);

        var query = new RequestQuery
        {
            RequestNumber = requestNumber,
            Statuses = statuses,
            RequestType = requestType,
            CreatedFrom = createdFrom,
            CreatedTo = createdTo,
            SortBy = sortBy,
            Direction = direction,
            PageSize = pageSize,
            After = cursor
        };

        var result = errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
        return (query, result);
    }

    private static IReadOnlyList<RequestStatus> ParseStatuses(
        string[]? rawStatuses, Dictionary<string, List<string>> errors)
    {
        if (rawStatuses is null || rawStatuses.Length == 0)
        {
            return [];
        }

        var parsed = new List<RequestStatus>();
        foreach (var value in rawStatuses)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (Enum.TryParse<RequestStatus>(value.Trim(), ignoreCase: true, out var status)
                && Enum.IsDefined(status))
            {
                if (!parsed.Contains(status))
                {
                    parsed.Add(status);
                }
            }
            else
            {
                AddError(errors, "status",
                    $"'{value}' is not a valid RequestStatus. Allowed: {AllowedStatuses}.");
            }
        }

        return parsed;
    }

    private static RequestType? ParseRequestType(
        string? rawType, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return null;
        }

        if (Enum.TryParse<RequestType>(rawType.Trim(), ignoreCase: true, out var type)
            && Enum.IsDefined(type))
        {
            return type;
        }

        AddError(errors, "requestType",
            $"'{rawType}' is not a valid RequestType. Allowed: {AllowedRequestTypes}.");
        return null;
    }

    private static DateTime? ParseDate(
        string? rawDate, string errorKey, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(rawDate))
        {
            return null;
        }

        if (DateTime.TryParse(
                rawDate.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return NormalizeToUtc(parsed);
        }

        AddError(errors, errorKey, $"'{rawDate}' is not a valid ISO 8601 date-time.");
        return null;
    }

    // Offset-less input is treated as already UTC; local/offset input is converted.
    private static DateTime NormalizeToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    private static SortField ParseSortField(
        string? rawSortBy, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(rawSortBy))
        {
            return SortField.CreatedAt;
        }

        if (Enum.TryParse<SortField>(rawSortBy.Trim(), ignoreCase: true, out var sortBy)
            && Enum.IsDefined(sortBy))
        {
            return sortBy;
        }

        AddError(errors, "sortBy",
            $"'{rawSortBy}' is not a valid sort field. Allowed: {AllowedSortFields}.");
        return SortField.CreatedAt;
    }

    private static SortDirection ParseSortDirection(
        string? rawDirection, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(rawDirection))
        {
            return SortDirection.Desc;
        }

        if (Enum.TryParse<SortDirection>(rawDirection.Trim(), ignoreCase: true, out var direction)
            && Enum.IsDefined(direction))
        {
            return direction;
        }

        AddError(errors, "sortDirection",
            $"'{rawDirection}' is not a valid sort direction. Allowed: {AllowedSortDirections}.");
        return SortDirection.Desc;
    }

    private static int ParsePageSize(
        string? rawPageSize, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(rawPageSize))
        {
            return DefaultPageSize;
        }

        if (int.TryParse(rawPageSize.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageSize)
            && pageSize >= MinPageSize
            && pageSize <= MaxPageSize)
        {
            return pageSize;
        }

        AddError(errors, "pageSize",
            $"pageSize must be an integer between {MinPageSize} and {MaxPageSize}.");
        return DefaultPageSize;
    }

    private static Cursor? ParseCursor(
        string? rawCursor,
        SortField sortBy,
        SortDirection direction,
        Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(rawCursor))
        {
            return null;
        }

        try
        {
            return CursorCodec.Decode(rawCursor.Trim(), sortBy, direction);
        }
        catch (CursorException ex) when (ex.Kind == CursorErrorKind.Malformed)
        {
            AddError(errors, "cursor", "The cursor value is invalid and could not be decoded.");
            return null;
        }
        catch (CursorException ex) when (ex.Kind == CursorErrorKind.SortMismatch)
        {
            AddError(errors, "cursor", "The cursor does not match the current sort field and direction.");
            return null;
        }
    }

    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            list = [];
            errors[key] = list;
        }

        list.Add(message);
    }
}
