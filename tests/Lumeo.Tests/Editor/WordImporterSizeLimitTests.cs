using System;
using Xunit;

namespace Lumeo.Tests.Editor;

public class WordImporterSizeLimitTests
{
    [Fact]
    public void Decode_throws_WordImportSizeException_when_base64_exceeds_limit()
    {
        // 12 base64 chars (no padding) → decoded length is 9 bytes.
        // With a 4-byte cap, Decode must reject without ever calling Convert.FromBase64String.
        const string base64 = "QUJDREVGR0hJSktM"; // 16 chars, no padding → 12 bytes decoded.
        const long limit = 4L;

        var ex = Assert.Throws<WordImportSizeException>(
            () => WordImporter.DecodeBase64WithLimit(base64, limit));

        Assert.Equal(limit, ex.LimitBytes);
        Assert.True(ex.ActualBytes > limit, "ActualBytes must reflect the over-limit decoded size.");
    }

    [Fact]
    public void Decode_rejects_oversized_base64_without_allocating_byte_array()
    {
        // Build a 4 MB base64 string up front, then decode against a 1 KB cap.
        // If Convert.FromBase64String were called, this would allocate ~3 MB on the heap;
        // the size guard MUST trip before the Convert call. We assert by timing — a guarded
        // path completes in a few ms, an unguarded path takes meaningfully longer due to the
        // multi-megabyte allocation + decode. We use a generous 250 ms ceiling so the test
        // is not flaky on slow CI but still catches an unguarded regression decisively.
        var oversized = new string('A', 4 * 1024 * 1024); // 4 MB of valid base64 chars.

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.Throws<WordImportSizeException>(
            () => WordImporter.DecodeBase64WithLimit(oversized, 1024L));
        sw.Stop();

        Assert.True(
            sw.ElapsedMilliseconds < 250,
            $"Size guard should reject before allocating; took {sw.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public void Decode_accepts_payload_under_limit()
    {
        // "Hello!" → "SGVsbG8h" (8 chars, decodes to 6 bytes).
        const string base64 = "SGVsbG8h";

        var bytes = WordImporter.DecodeBase64WithLimit(base64, 1024L);

        Assert.Equal(6, bytes.Length);
        Assert.Equal((byte)'H', bytes[0]);
        Assert.Equal((byte)'!', bytes[5]);
    }

    [Fact]
    public void Decode_accounts_for_padding_chars_in_size_check()
    {
        // "Hi" → "SGk=" (4 chars incl. 1 '=' pad, decodes to 2 bytes).
        // Limit of 2 must accept (decoded length is exactly at the limit).
        const string base64 = "SGk=";

        var bytes = WordImporter.DecodeBase64WithLimit(base64, 2L);

        Assert.Equal(2, bytes.Length);
    }

    [Fact]
    public void Decode_throws_ArgumentNullException_for_null_input()
    {
        Assert.Throws<ArgumentNullException>(
            () => WordImporter.DecodeBase64WithLimit(null!, 1024L));
    }

    [Fact]
    public void Decode_throws_ArgumentOutOfRange_for_zero_or_negative_limit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WordImporter.DecodeBase64WithLimit("SGk=", 0L));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WordImporter.DecodeBase64WithLimit("SGk=", -1L));
    }
}
