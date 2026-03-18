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
        var formatVersion = table.PrimaryKey.Count > 0 || table.ForeignKeys.Count > 0 || table.UniqueColumns.Count > 0 ? 2 : FormatVersion;
        var lines = new List<string>
        {
            $"TABLE: {table.TableName}",
            $"FORMAT_VERSION: {formatVersion}",
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
            var pk = col.IsPrimaryKey ? "|PK" : "";
            var uq = col.IsUnique ? "|UQ" : "";
            lines.Add($"{i + 1}|{col.Name}|{typeStr}{pk}{uq}");
        }

        if (table.MaxShardSize.HasValue)
            lines.Add("MAX_SHARD_SIZE: " + table.MaxShardSize.Value);
        if (table.PrimaryKey.Count > 0)
            lines.Add("PRIMARY_KEY: " + string.Join(",", table.PrimaryKey));
        foreach (var fk in table.ForeignKeys)
            lines.Add($"FOREIGN_KEY: {fk.ColumnName}|{fk.ReferencedTable}|{fk.ReferencedColumn}");
        if (table.UniqueColumns.Count > 0)
            lines.Add("UNIQUE: " + string.Join(",", table.UniqueColumns));
        foreach (var idx in table.Indexes)
            lines.Add($"INDEX: {idx.IndexName}|{string.Join(",", idx.ColumnNames)}|{(idx.IsUnique ? "1" : "0")}");

        return string.Join(Environment.NewLine, lines);
    }

    private static TableDefinition ParseSchema(string content, string tableName)
    {
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var columns = new List<ColumnDefinition>();
        var primaryKeyColumns = new List<string>();
        var foreignKeyDefinitions = new List<ForeignKeyDefinition>();
        var uniqueConstraintColumns = new List<string>();
        var indexDefinitions = new List<IndexDefinition>();
        long? maxShardSize = null;
        var inColumns = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("MAX_SHARD_SIZE:"))
            {
                var val = line["MAX_SHARD_SIZE:".Length..].Trim();
                if (long.TryParse(val, out var ms))
                    maxShardSize = ms;
                continue;
            }
            if (line.StartsWith("COLUMNS:"))
            {
                inColumns = true;
                continue;
            }

            if (line.StartsWith("PRIMARY_KEY:"))
            {
                var val = line["PRIMARY_KEY:".Length..].Trim();
                if (!string.IsNullOrEmpty(val))
                    primaryKeyColumns.AddRange(val.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));
                continue;
            }

            if (line.StartsWith("FOREIGN_KEY:"))
            {
                var val = line["FOREIGN_KEY:".Length..].Trim();
                var parts = val.Split('|');
                if (parts.Length >= 3)
                    foreignKeyDefinitions.Add(new ForeignKeyDefinition(parts[0].Trim(), parts[1].Trim(), parts[2].Trim()));
                continue;
            }

            if (line.StartsWith("UNIQUE:"))
            {
                var val = line["UNIQUE:".Length..].Trim();
                if (!string.IsNullOrEmpty(val))
                    uniqueConstraintColumns.AddRange(val.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));
                continue;
            }

            if (line.StartsWith("INDEX:"))
            {
                var val = line["INDEX:".Length..].Trim();
                var parts = val.Split('|');
                if (parts.Length >= 2)
                {
                    var idxName = parts[0].Trim();
                    var cols = parts[1].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    var isUnique = parts.Length > 2 && parts[2].Trim() == "1";
                    indexDefinitions.Add(new IndexDefinition(idxName, tableName, cols, isUnique));
                }
                continue;
            }

            if (inColumns && line.Contains('|'))
            {
                var parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    var colName = parts[1];
                    var typeStr = parts[2].ToUpperInvariant();
                    var isPk = parts.Contains("PK", StringComparer.OrdinalIgnoreCase);
                    var isUq = parts.Contains("UQ", StringComparer.OrdinalIgnoreCase);
                    ColumnType type;
                    int? width = null;
                    int? scale = null;

                    switch (typeStr)
                    {
                        case "CHAR":
                            type = ColumnType.Char;
                            width = parts.Length > 3 && int.TryParse(parts[3], out var w) ? w : 0;
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

                    columns.Add(new ColumnDefinition(colName, type, width, scale, isPk, isUq));
                }
            }
        }

        return new TableDefinition(
            tableName,
            columns,
            MaxShardSize: maxShardSize,
            PrimaryKeyColumns: primaryKeyColumns.Count > 0 ? primaryKeyColumns : null,
            ForeignKeyDefinitions: foreignKeyDefinitions.Count > 0 ? foreignKeyDefinitions : null,
            UniqueConstraintColumns: uniqueConstraintColumns.Count > 0 ? uniqueConstraintColumns : null,
            IndexDefinitions: indexDefinitions.Count > 0 ? indexDefinitions : null);
    }
}
