using Requests.Application.Requests;
using Xunit;

namespace Requests.Tests;

public class CursorCodecTests
{
    // ---------------------------------------------------------------------
    // 1. Round-trip: Encode then Decode preserves all fields.
    //    Validates design Property 5: Decode(Encode(cursor)) == cursor
    //    (Requirements 7.4, 7.9)
    // ---------------------------------------------------------------------

    public static IEnumerable<object[]> RoundTripCursors()
    {
        // Various sort fields / directions and LastValue shapes.
        yield return new object[] { new Cursor(SortField.CreatedAt, SortDirection.Desc, "2024-05-01T10:00:00Z", 12) };
        yield return new object[] { new Cursor(SortField.CreatedAt, SortDirection.Asc, "2020-01-01T00:00:00.0000000Z", 1) };
        yield return new object[] { new Cursor(SortField.RequestNumber, SortDirection.Asc, "REQ-000042", 42) };
        yield return new object[] { new Cursor(SortField.RequestNumber, SortDirection.Desc, "REQ-999999", int.MaxValue) };
        yield return new object[] { new Cursor(SortField.Status, SortDirection.Asc, "New", 7) };
        yield return new object[] { new Cursor(SortField.Status, SortDirection.Desc, "Cancelled", 0) };
        yield return new object[] { new Cursor(SortField.RequestType, SortDirection.Asc, "Legal", 100) };
        yield return new object[] { new Cursor(SortField.RequestType, SortDirection.Desc, "Appeal", -5) };
        // Empty-ish and special-character LastValue strings.
        yield return new object[] { new Cursor(SortField.RequestNumber, SortDirection.Asc, string.Empty, 3) };
        yield return new object[] { new Cursor(SortField.RequestNumber, SortDirection.Asc, "   ", 4) };
        yield return new object[] { new Cursor(SortField.RequestNumber, SortDirection.Asc, "value with spaces & symbols +/=", 55) };
        yield return new object[] { new Cursor(SortField.RequestNumber, SortDirection.Asc, "יחידה", 61) }; // non-ASCII
    }

    [Theory]
    [MemberData(nameof(RoundTripCursors))]
    public void Decode_OfEncoded_PreservesAllFields(Cursor original)
    {
        var token = CursorCodec.Encode(original);

        var decoded = CursorCodec.Decode(token);

        Assert.Equal(original.SortBy, decoded.SortBy);
        Assert.Equal(original.Direction, decoded.Direction);
        Assert.Equal(original.LastValue, decoded.LastValue);
        Assert.Equal(original.LastId, decoded.LastId);
    }

    [Theory]
    [MemberData(nameof(RoundTripCursors))]
    public void Decode_OfEncoded_EqualsOriginalRecord(Cursor original)
    {
        // Record value-equality is a concise expression of Property 5.
        var decoded = CursorCodec.Decode(CursorCodec.Encode(original));

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Encode_ProducesUrlSafeToken_WithoutPadding()
    {
        var token = CursorCodec.Encode(
            new Cursor(SortField.CreatedAt, SortDirection.Desc, "2024-05-01T10:00:00Z", 12));

        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void Encode_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CursorCodec.Encode(null!));
    }

    // ---------------------------------------------------------------------
    // 2. Malformed cursor -> CursorException(Kind == Malformed) (Requirement 7.9)
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Decode_NullEmptyOrWhitespace_ThrowsMalformed(string? token)
    {
        var ex = Assert.Throws<CursorException>(() => CursorCodec.Decode(token!));

        Assert.Equal(CursorErrorKind.Malformed, ex.Kind);
    }

    [Theory]
    [InlineData("!!!not-base64!!!")]      // characters outside the Base64url alphabet
    [InlineData("@#$%^&*()")]             // pure garbage
    [InlineData("a")]                     // length 1 mod 4 is never valid Base64
    [InlineData("abcde")]                 // length 1 mod 4 (5 chars)
    public void Decode_GarbageOrInvalidLength_ThrowsMalformed(string token)
    {
        var ex = Assert.Throws<CursorException>(() => CursorCodec.Decode(token));

        Assert.Equal(CursorErrorKind.Malformed, ex.Kind);
    }

    [Fact]
    public void Decode_ValidBase64ButNotCursorJson_ThrowsMalformed()
    {
        // Valid Base64url that decodes to JSON which is not a cursor payload.
        var token = EncodeJson("{\"foo\":123}");

        var ex = Assert.Throws<CursorException>(() => CursorCodec.Decode(token));

        Assert.Equal(CursorErrorKind.Malformed, ex.Kind);
    }

    [Fact]
    public void Decode_ValidBase64ButNotJson_ThrowsMalformed()
    {
        // Valid Base64url bytes that are not JSON at all.
        var token = ToBase64Url(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        var ex = Assert.Throws<CursorException>(() => CursorCodec.Decode(token));

        Assert.Equal(CursorErrorKind.Malformed, ex.Kind);
    }

    [Fact]
    public void Decode_PayloadWithUndefinedEnum_ThrowsMalformed()
    {
        // SortBy value 999 is not a defined SortField.
        var token = EncodeJson("{\"s\":999,\"d\":0,\"v\":\"x\",\"i\":1}");

        var ex = Assert.Throws<CursorException>(() => CursorCodec.Decode(token));

        Assert.Equal(CursorErrorKind.Malformed, ex.Kind);
    }

    // ---------------------------------------------------------------------
    // 3. Sort-mismatch -> CursorException(Kind == SortMismatch) (Requirement 7.10)
    // ---------------------------------------------------------------------

    [Fact]
    public void Decode_WithMismatchedSortField_ThrowsSortMismatch()
    {
        var token = CursorCodec.Encode(
            new Cursor(SortField.CreatedAt, SortDirection.Desc, "2024-05-01T10:00:00Z", 12));

        var ex = Assert.Throws<CursorException>(
            () => CursorCodec.Decode(token, SortField.RequestNumber, SortDirection.Desc));

        Assert.Equal(CursorErrorKind.SortMismatch, ex.Kind);
    }

    [Fact]
    public void Decode_WithMismatchedSortDirection_ThrowsSortMismatch()
    {
        var token = CursorCodec.Encode(
            new Cursor(SortField.CreatedAt, SortDirection.Desc, "2024-05-01T10:00:00Z", 12));

        var ex = Assert.Throws<CursorException>(
            () => CursorCodec.Decode(token, SortField.CreatedAt, SortDirection.Asc));

        Assert.Equal(CursorErrorKind.SortMismatch, ex.Kind);
    }

    [Fact]
    public void Decode_WithMatchingSort_ReturnsCursor()
    {
        var original = new Cursor(SortField.Status, SortDirection.Asc, "New", 7);
        var token = CursorCodec.Encode(original);

        var decoded = CursorCodec.Decode(token, SortField.Status, SortDirection.Asc);

        Assert.Equal(original, decoded);
    }

    // ---------------------------------------------------------------------
    // 4. Distinctness: the two failure modes are distinguishable via Kind.
    // ---------------------------------------------------------------------

    [Fact]
    public void FailureModes_AreDistinguishable_ViaKind()
    {
        // Malformed: undecodable token.
        var malformed = Assert.Throws<CursorException>(() => CursorCodec.Decode("!!!bad!!!"));

        // SortMismatch: decodable token but wrong sort.
        var validToken = CursorCodec.Encode(
            new Cursor(SortField.CreatedAt, SortDirection.Desc, "2024-05-01T10:00:00Z", 12));
        var mismatch = Assert.Throws<CursorException>(
            () => CursorCodec.Decode(validToken, SortField.Status, SortDirection.Asc));

        Assert.Equal(CursorErrorKind.Malformed, malformed.Kind);
        Assert.Equal(CursorErrorKind.SortMismatch, mismatch.Kind);
        Assert.NotEqual(malformed.Kind, mismatch.Kind);
    }

    // ---------------------------------------------------------------------
    // Helpers — mirror the codec's Base64url scheme (URL-safe, no padding).
    // ---------------------------------------------------------------------

    private static string EncodeJson(string json)
        => ToBase64Url(System.Text.Encoding.UTF8.GetBytes(json));

    private static string ToBase64Url(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
