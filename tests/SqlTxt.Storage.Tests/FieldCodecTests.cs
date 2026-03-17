using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class FieldCodecTests
{
    [Fact]
    public void Encode_Backslash_EscapesToDoubleBackslash()
    {
        Assert.Equal(@"\\", FieldCodec.Encode(@"\"));
    }

    [Fact]
    public void Encode_Newline_EscapesToBackslashN()
    {
        Assert.Equal(@"\n", FieldCodec.Encode("\n"));
    }

    [Fact]
    public void Encode_CarriageReturn_EscapesToBackslashR()
    {
        Assert.Equal(@"\r", FieldCodec.Encode("\r"));
    }

    [Fact]
    public void Encode_Tab_EscapesToBackslashT()
    {
        Assert.Equal(@"\t", FieldCodec.Encode("\t"));
    }

    [Fact]
    public void Encode_Mixed_EscapesAllSpecialChars()
    {
        Assert.Equal(@"a\nb\tc\\d", FieldCodec.Encode("a\nb\tc\\d"));
    }

    [Fact]
    public void Decode_BackslashN_YieldsNewline()
    {
        Assert.Equal("\n", FieldCodec.Decode(@"\n"));
    }

    [Fact]
    public void Decode_BackslashT_YieldsTab()
    {
        Assert.Equal("\t", FieldCodec.Decode(@"\t"));
    }

    [Fact]
    public void Decode_BackslashR_YieldsCarriageReturn()
    {
        Assert.Equal("\r", FieldCodec.Decode(@"\r"));
    }

    [Fact]
    public void Decode_DoubleBackslash_YieldsSingleBackslash()
    {
        Assert.Equal(@"\", FieldCodec.Decode(@"\\"));
    }

    [Fact]
    public void Decode_UnknownEscape_YieldsLiteralChar()
    {
        Assert.Equal("x", FieldCodec.Decode(@"\x"));
    }

    [Fact]
    public void EncodeDecode_RoundTrips()
    {
        var original = "Line1\nLine2\tTabbed\\Escaped";
        var encoded = FieldCodec.Encode(original);
        var decoded = FieldCodec.Decode(encoded);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void TruncateToWidth_WithinWidth_ReturnsAsIs()
    {
        var result = FieldCodec.TruncateToWidth("abc", 5, null, "T", "C");
        Assert.Equal("abc", result);
    }

    [Fact]
    public void TruncateToWidth_ExceedsWidth_Truncates()
    {
        var result = FieldCodec.TruncateToWidth("abcdef", 4, null, "T", "C");
        Assert.Equal("abcd", result);
    }

    [Fact]
    public void TruncateToWidth_NeverSplitsEscapeSequence()
    {
        // "a\n" = 3 chars encoded; width 3 would split \n; we truncate to "a" only
        var result = FieldCodec.TruncateToWidth(@"a\nb", 2, null, "T", "C");
        Assert.Equal("a", result);
    }

    [Fact]
    public void TruncateToWidth_AddsWarningWhenTruncated()
    {
        var warnings = new List<string>();
        FieldCodec.TruncateToWidth("abcdef", 3, warnings, "MyTable", "MyCol");
        Assert.Single(warnings);
        Assert.Contains("Truncated", warnings[0]);
        Assert.Contains("MyTable.MyCol", warnings[0]);
    }
}
