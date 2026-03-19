using System.Text.Json;
using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Reads MVCC flag from db/manifest.json.
/// </summary>
public static class MvccManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<bool> ReadMvccEnabledAsync(
        IFileSystemAccessor fs,
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        var path = fs.Combine(databasePath, "db", "manifest.json");
        if (!fs.FileExists(path))
            return false;
        var json = await fs.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("mvcc", out var mvcc))
                return mvcc.GetBoolean();
        }
        catch (JsonException)
        {
            return false;
        }
        return false;
    }
}
