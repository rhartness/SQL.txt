using SqlTxt.Contracts;
using SqlTxt.Engine;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDatabaseEngine>(_ => new DatabaseEngine());
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapPost("/exec", async (ExecRequest req, IDatabaseEngine engine, IConfiguration config) =>
{
    var dbPath = req.DatabasePath ?? config["SqlTxt:DatabasePath"] ?? ".";
    var result = await engine.ExecuteAsync(req.Sql, dbPath);
    return Results.Ok(new ExecResponse(result.RowsAffected, result.Warnings));
});

app.MapPost("/query", async (QueryRequest req, IDatabaseEngine engine, IConfiguration config) =>
{
    var dbPath = req.DatabasePath ?? config["SqlTxt:DatabasePath"] ?? ".";
    var result = await engine.ExecuteQueryAsync(req.Sql, dbPath);
    return Results.Ok(new QueryResponse(
        result.QueryResult?.ColumnNames ?? Array.Empty<string>(),
        result.QueryResult?.Rows.Select(r => r.Values).ToList() ?? new List<IReadOnlyDictionary<string, string>>(),
        result.Warnings));
});

app.MapPost("/rebalance", async (RebalanceRequest req, IDatabaseEngine engine, IConfiguration config) =>
{
    var dbPath = req.DatabasePath ?? config["SqlTxt:DatabasePath"] ?? ".";
    var count = await engine.RebalanceTableAsync(dbPath, req.TableName);
    return Results.Ok(new RebalanceResponse(count));
});

await app.RunAsync();

public record ExecRequest(string Sql, string? DatabasePath = null);
public record ExecResponse(int RowsAffected, IReadOnlyList<string>? Warnings);

public record QueryRequest(string Sql, string? DatabasePath = null);
public record QueryResponse(IReadOnlyList<string> ColumnNames, IReadOnlyList<IReadOnlyDictionary<string, string>> Rows, IReadOnlyList<string>? Warnings);

public record RebalanceRequest(string TableName, string? DatabasePath = null);
public record RebalanceResponse(int RowsProcessed);

public partial class Program { }
