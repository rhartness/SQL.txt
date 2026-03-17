namespace SqlTxt.Storage;

/// <summary>
/// Encodes and decodes CHAR field values for storage (escape sequences for newlines, tabs, etc.)
/// and truncates to width without splitting escape sequences.
/// </summary>
public static class FieldCodec
{
    /// <summary>
    /// Encodes raw value: \ -> \\, \n -> \n, \r -> \r, \t -> \t.
    /// </summary>
    public static string Encode(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var sb = new System.Text.StringBuilder(value.Length * 2);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\': sb.Append(@"\\"); break;
                case '\n': sb.Append(@"\n"); break;
                case '\r': sb.Append(@"\r"); break;
                case '\t': sb.Append(@"\t"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Decodes encoded value: \\ -> \, \n -> newline, \r -> cr, \t -> tab.
    /// Unknown escapes (e.g. \x) yield literal x.
    /// </summary>
    public static string Decode(string encoded)
    {
        if (string.IsNullOrEmpty(encoded))
            return encoded;

        var sb = new System.Text.StringBuilder(encoded.Length);
        for (var i = 0; i < encoded.Length; i++)
        {
            var ch = encoded[i];
            if (ch == '\\' && i + 1 < encoded.Length)
            {
                var next = encoded[i + 1];
                switch (next)
                {
                    case '\\': sb.Append('\\'); i++; break;
                    case 'n': sb.Append('\n'); i++; break;
                    case 'r': sb.Append('\r'); i++; break;
                    case 't': sb.Append('\t'); i++; break;
                    default: sb.Append(next); i++; break;
                }
            }
            else
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Truncates encoded string to width. Never splits escape sequences.
    /// When truncation occurs, adds a warning if warnings list is provided.
    /// </summary>
    public static string TruncateToWidth(string encoded, int width, List<string>? warnings, string tableName, string columnName)
    {
        if (encoded.Length <= width)
            return encoded;

        var result = new System.Text.StringBuilder(width);
        var i = 0;
        while (i < encoded.Length && result.Length < width)
        {
            var ch = encoded[i];
            if (ch == '\\' && i + 1 < encoded.Length)
            {
                var next = encoded[i + 1];
                var seqLen = 2; // \n, \r, \t, \\, or \x
                if (result.Length + seqLen <= width)
                {
                    result.Append(ch).Append(next);
                    i += seqLen;
                }
                else
                    break;
            }
            else
            {
                result.Append(ch);
                i++;
            }
        }

        var truncated = result.ToString();
        if (truncated.Length < encoded.Length)
            warnings?.Add($"Truncated column '{tableName}.{columnName}' from {encoded.Length} to {truncated.Length} characters.");

        return truncated;
    }
}
