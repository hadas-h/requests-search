using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Requests.Application.Requests;

/// <summary>
/// Encodes/decodes a <see cref="Cursor"/> to and from an opaque Base64url token (JSON payload,
/// URL-safe alphabet, padding stripped). Decoding failures throw <see cref="CursorException"/>
/// with a <see cref="CursorErrorKind"/> so the validator can map each to a distinct HTTP 400.
/// </summary>
public static class CursorCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string Encode(Cursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        var payload = new CursorPayload(cursor.SortBy, cursor.Direction, cursor.LastValue, cursor.LastId);
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
        return ToBase64Url(json);
    }

    public static Cursor Decode(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new CursorException(CursorErrorKind.Malformed, "The cursor value is missing or empty.");
        }

        byte[] bytes;
        try
        {
            bytes = FromBase64Url(token);
        }
        catch (FormatException ex)
        {
            throw new CursorException(CursorErrorKind.Malformed, "The cursor value is not a valid token.", ex);
        }

        CursorPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<CursorPayload>(bytes, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new CursorException(CursorErrorKind.Malformed, "The cursor value could not be decoded.", ex);
        }

        if (payload is null || payload.LastValue is null
            || !Enum.IsDefined(payload.SortBy) || !Enum.IsDefined(payload.Direction))
        {
            throw new CursorException(CursorErrorKind.Malformed, "The cursor value could not be decoded.");
        }

        return new Cursor(payload.SortBy, payload.Direction, payload.LastValue, payload.LastId);
    }

    /// <summary>Decodes a token and rejects it if its sort field/direction differ from the active sort.</summary>
    public static Cursor Decode(string token, SortField expectedSortBy, SortDirection expectedDirection)
    {
        var cursor = Decode(token);

        if (cursor.SortBy != expectedSortBy || cursor.Direction != expectedDirection)
        {
            throw new CursorException(
                CursorErrorKind.SortMismatch,
                "The cursor does not match the current sort order.");
        }

        return cursor;
    }

    private static string ToBase64Url(byte[] bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        var builder = new StringBuilder(base64.Length);
        foreach (var c in base64)
        {
            switch (c)
            {
                case '+': builder.Append('-'); break;
                case '/': builder.Append('_'); break;
                case '=': break;
                default: builder.Append(c); break;
            }
        }

        return builder.ToString();
    }

    private static byte[] FromBase64Url(string token)
    {
        var builder = new StringBuilder(token.Length + 3);
        foreach (var c in token)
        {
            switch (c)
            {
                case '-': builder.Append('+'); break;
                case '_': builder.Append('/'); break;
                default: builder.Append(c); break;
            }
        }

        switch (builder.Length % 4)
        {
            case 2: builder.Append("=="); break;
            case 3: builder.Append('='); break;
            case 1: throw new FormatException("Invalid Base64url length.");
        }

        return Convert.FromBase64String(builder.ToString());
    }

    private sealed record CursorPayload(
        [property: JsonPropertyName("s")] SortField SortBy,
        [property: JsonPropertyName("d")] SortDirection Direction,
        [property: JsonPropertyName("v")] string LastValue,
        [property: JsonPropertyName("i")] int LastId);
}
