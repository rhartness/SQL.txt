namespace SqlTxt.Engine;

/// <summary>
/// Splits SQL script text into batches and statements.
/// Respects string literals and comments. Splits on ; (statement terminator) and GO (batch separator).
/// </summary>
public static class ScriptSplitter
{
    /// <summary>
    /// Splits script into batches. Each batch is a list of statements (split by ;).
    /// GO on its own line starts a new batch.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> SplitIntoBatches(string script)
    {
        var batches = new List<List<string>>();
        var currentBatch = new List<string>();
        var currentStatement = new System.Text.StringBuilder();

        var i = 0;
        while (i < script.Length)
        {
            if (IsAtLineStart(script, i) && IsGoKeyword(script, i))
            {
                FlushStatement(currentStatement, currentBatch);
                if (currentBatch.Count > 0)
                {
                    batches.Add(currentBatch);
                    currentBatch = new List<string>();
                }
                i = SkipGoAndRestOfLine(script, i);
                continue;
            }

            var ch = script[i];

            if (ch == '\'' || ch == '"')
            {
                var quote = ch;
                currentStatement.Append(ch);
                i++;
                while (i < script.Length)
                {
                    var c = script[i];
                    if (c == quote && (i == 0 || script[i - 1] != '\\'))
                    {
                        currentStatement.Append(c);
                        i++;
                        break;
                    }
                    if (c == '\\' && i + 1 < script.Length)
                    {
                        currentStatement.Append(c).Append(script[i + 1]);
                        i += 2;
                        continue;
                    }
                    currentStatement.Append(c);
                    i++;
                }
                continue;
            }

            if (ch == '-' && i + 1 < script.Length && script[i + 1] == '-')
            {
                while (i < script.Length && script[i] != '\n' && script[i] != '\r')
                {
                    i++;
                }
                continue;
            }

            if (ch == '/' && i + 1 < script.Length && script[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < script.Length && !(script[i] == '*' && script[i + 1] == '/'))
                {
                    i++;
                }
                if (i + 1 < script.Length)
                    i += 2;
                continue;
            }

            if (ch == ';')
            {
                FlushStatement(currentStatement, currentBatch);
                i++;
                continue;
            }

            currentStatement.Append(ch);
            i++;
        }

        FlushStatement(currentStatement, currentBatch);

        if (currentBatch.Count > 0)
            batches.Add(currentBatch);

        return batches;
    }

    private static void FlushStatement(System.Text.StringBuilder sb, List<string> batch)
    {
        var s = sb.ToString().Trim();
        if (s.Length > 0)
            batch.Add(s);
        sb.Clear();
    }

    private static bool IsAtLineStart(string script, int i)
    {
        if (i == 0)
            return true;
        var prev = script[i - 1];
        return prev == '\n' || prev == '\r';
    }

    private static bool IsGoKeyword(string script, int i)
    {
        if (i + 2 > script.Length)
            return false;
        var g = script[i];
        var o = script[i + 1];
        if (char.ToUpperInvariant(g) != 'G' || char.ToUpperInvariant(o) != 'O')
            return false;
        var next = i + 2;
        while (next < script.Length)
        {
            var c = script[next];
            if (c == ' ' || c == '\t')
            {
                next++;
                continue;
            }
            if (c == '\n' || c == '\r' || c == '-')
                return true;
            if (char.IsDigit(c))
            {
                while (next < script.Length && char.IsDigit(script[next]))
                    next++;
                return next >= script.Length || script[next] == '\n' || script[next] == '\r' || script[next] == ' ' || script[next] == '\t';
            }
            return false;
        }
        return true;
    }

    private static int SkipGoAndRestOfLine(string script, int i)
    {
        while (i < script.Length && script[i] != '\n' && script[i] != '\r')
            i++;
        if (i < script.Length && script[i] == '\r')
            i++;
        if (i < script.Length && script[i] == '\n')
            i++;
        return i;
    }
}
