using SqlTxt.Contracts;

namespace SqlTxt.Contracts.Commands;

/// <summary>
/// Command to create a new table.
/// </summary>
/// <param name="Table">Table definition including columns.</param>
public sealed record CreateTableCommand(TableDefinition Table);
