using SqlTxt.Contracts;
using SqlTxt.Contracts.Exceptions;

namespace SqlTxt.Storage;

/// <summary>
/// Persists schema to ~System (master) and table folder (reference copy).
/// </summary>
public sealed class SchemaStore : ISchemaStore
{
    private const string SchemaFileName = "schema.txt";
    private const int FormatVersion = 1;

    private readonly Contracts.IFileSystemAccessor _fs;

    public SchemaStore(Contracts.IFileSystemAccessor fs) => _fs = fs;

    public async Task WriteSchemaAsync(string databasePath, TableDefinition table, CancellationToken cancellationToken = default)
    {
        var content = SerializeSchema(table);
        var systemPath = _fs.Combine(databasePath, "~System", "schemas", table.TableName + ".txt");
        var tablePath = _fs.Combine(databasePath, "Tables", table.TableName, SchemaFileName);

        _fs.CreateDirectory(Path.GetDirectoryName(systemPath)!);
        _fs.CreateDirectory(Path.GetDirectoryName(tablePath)!);

        await _fs.WriteAllTextAsync(systemPath, content, cancellationToken).ConfigureAwait(false);
        await _fs.WriteAllTextAsync(tablePath, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TableDefinition?> ReadSchemaAsync(string databasePath, string tableName, CancellationToken cancellationToken = default)
    {
        var systemPath = _fs.Combine(databasePath, "~System", "schemas", tableName + ".txt");
        if (!_fs.FileExists(systemPath))
            return null;

        var content = await _fs.ReadAllTextAsync(systemPath, cancellationToken).ConfigureAwait(false);
        return ParseSchema(content, tableName);
    }

    public async Task<IReadOnlyList<string>> GetTableNamesAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var tablesPath = _fs.Combine(databasePath, "Tables");
        if (!_fs.DirectoryExists(tablesPath))
            return Array.Empty<string>();

        var dirs = _fs.GetDirectories(tablesPath);
        var names = new List<string>();
        foreach (var dir in dirs)
        {
            var name = Path.GetFileName(dir);
            if (!string.IsNullOrEmpty(name) && !name.StartsWith("~"))
                names.Add(name);
        }
        return await Task.FromResult(names).ConfigureAwait(false);
    }

    private static string SerializeSchema(TableDefinition table)
    {
        var lines = new List<string>
        {
            $"TABLE: {table.TableName}",
            $"FORMAT_VERSION: {FormatVersion}",
            "COLUMNS:"
        };

        for (var i = 0; i < table.Columns.Count; i++)
        {
            var col = table.Columns[i];
            var typeStr = col.Type switch
            {
                ColumnType.Char => $"CHAR|{col.Width ?? 0}",
                ColumnType.Int => "INT",
                ColumnType.TinyInt => "TINYINT",
                ColumnType.BigInt => "BIGINT",
                ColumnType.Bit => "BIT",
                ColumnType.Decimal => $"DECIMAL|{col.Width ?? 0}|{col.Scale ?? 0}",
                _ => "CHAR|0"
            };
            lines.Add($"{i + 1}|{col.Name}|{typeStr}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static TableDefinition ParseSchema(string content, string tableName)
    {
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var columns = new List<ColumnDefinition>();
        var inColumns = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("COLUMNS:"))
            {
                inColumns = true;
                continue;
            }

            if (inColumns && line.Contains('|'))
            {
                var parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    var colName = parts[1];
                    var typeStr = parts[2].ToUpperInvariant();
                    ColumnType type;
                    int? width = null;
                    int? scale = null;

                    switch (typeStr)
                    {
                        case "CHAR":
                            type = ColumnType.Char;
                            width = parts.Length > 3 ? int.Parse(parts[3]) : 0;
                            break;
                        case "INT":
                            type = ColumnType.Int;
                            break;
                        case "TINYINT":
                            type = ColumnType.TinyInt;
                            break;
                        case "BIGINT":
                            type = ColumnType.BigInt;
                            break;
                        case "BIT":
                            type = ColumnType.Bit;
                            break;
                        case "DECIMAL":
                            type = ColumnType.Decimal;
                            width = parts.Length > 3 ? int.Parse(parts[3]) : 0;
                            scale = parts.Length > 4 ? int.Parse(parts[4]) : 0;
                            break;
                        default:
                            type = ColumnType.Char;
                            width = 0;
                            break;
                    }

                    columns.Add(new ColumnDefinition(colName, type, width, scale));
                }
            }
        }

        return new TableDefinition(tableName, columns);
    }
}
