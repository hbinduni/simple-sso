namespace Server.Common;

/// <summary>
/// TypeID generation: a type-prefixed, K-sortable identifier of the form
/// {prefix}_{26-char Crockford base32 of a UUIDv7}. Matches the format produced
/// by the Go go.jetify.com/typeid library that the previous server used.
/// </summary>
public static class TypeId
{
    // Crockford base32, lowercase — same alphabet the TypeID spec mandates.
    private const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    public static string NewUserId() => New("user");
    public static string NewItemId() => New("item");
    public static string NewSessionId() => New("sess");
    public static string NewOAuthAccountId() => New("oauth");

    public static string New(string prefix)
    {
        var guid = Guid.CreateVersion7();
        Span<byte> bytes = stackalloc byte[16];
        guid.TryWriteBytes(bytes, bigEndian: true, out _);
        return $"{prefix}_{EncodeBase32(bytes)}";
    }

    /// <summary>Loose validation: a non-empty prefix, an underscore, a non-empty suffix.</summary>
    public static bool IsValid(string id)
    {
        var idx = id.IndexOf('_');
        return idx > 0 && idx < id.Length - 1;
    }

    // 16 bytes (128 bits) -> 26 base32 chars. Bit layout per the TypeID spec.
    private static string EncodeBase32(ReadOnlySpan<byte> b)
    {
        Span<char> c = stackalloc char[26];
        c[0]  = Alphabet[(b[0] & 0xE0) >> 5];
        c[1]  = Alphabet[b[0] & 0x1F];
        c[2]  = Alphabet[(b[1] & 0xF8) >> 3];
        c[3]  = Alphabet[((b[1] & 0x07) << 2) | ((b[2] & 0xC0) >> 6)];
        c[4]  = Alphabet[(b[2] & 0x3E) >> 1];
        c[5]  = Alphabet[((b[2] & 0x01) << 4) | ((b[3] & 0xF0) >> 4)];
        c[6]  = Alphabet[((b[3] & 0x0F) << 1) | ((b[4] & 0x80) >> 7)];
        c[7]  = Alphabet[(b[4] & 0x7C) >> 2];
        c[8]  = Alphabet[((b[4] & 0x03) << 3) | ((b[5] & 0xE0) >> 5)];
        c[9]  = Alphabet[b[5] & 0x1F];
        c[10] = Alphabet[(b[6] & 0xF8) >> 3];
        c[11] = Alphabet[((b[6] & 0x07) << 2) | ((b[7] & 0xC0) >> 6)];
        c[12] = Alphabet[(b[7] & 0x3E) >> 1];
        c[13] = Alphabet[((b[7] & 0x01) << 4) | ((b[8] & 0xF0) >> 4)];
        c[14] = Alphabet[((b[8] & 0x0F) << 1) | ((b[9] & 0x80) >> 7)];
        c[15] = Alphabet[(b[9] & 0x7C) >> 2];
        c[16] = Alphabet[((b[9] & 0x03) << 3) | ((b[10] & 0xE0) >> 5)];
        c[17] = Alphabet[b[10] & 0x1F];
        c[18] = Alphabet[(b[11] & 0xF8) >> 3];
        c[19] = Alphabet[((b[11] & 0x07) << 2) | ((b[12] & 0xC0) >> 6)];
        c[20] = Alphabet[(b[12] & 0x3E) >> 1];
        c[21] = Alphabet[((b[12] & 0x01) << 4) | ((b[13] & 0xF0) >> 4)];
        c[22] = Alphabet[((b[13] & 0x0F) << 1) | ((b[14] & 0x80) >> 7)];
        c[23] = Alphabet[(b[14] & 0x7C) >> 2];
        c[24] = Alphabet[((b[14] & 0x03) << 3) | ((b[15] & 0xE0) >> 5)];
        c[25] = Alphabet[b[15] & 0x1F];
        return new string(c);
    }
}
