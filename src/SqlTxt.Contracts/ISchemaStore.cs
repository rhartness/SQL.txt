namespace SqlTxt.Contracts;

/// <summary>
/// Reads and writes table schema definitions.
/// </summary>
public interface ISchemaStore
{
    /// <summary>
    /// Writes schema to both ~System (master) and table folder (reference copy).
    /// </summary>
    /// <param name="databasePath">Path to database root.</param>
    /// <param name="table">Table definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteSchemaAsync(string databasePath, TableDefinition table, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads schema from ~System (master).
    /// </summary>
    /// <param name="databasePath">Path to database root.</param>
    /// <param name="tableName">Table name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Table definition, or null if not found.</returns>
    Task<TableDefinition?> ReadSchemaAsync(string databasePath, string tableName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all table names in the database.
    /// </summary>
    Task<IReadOnlyList<string>> GetTableNamesAsync(string databasePath, CancellationToken cancellationToken = default);
}
