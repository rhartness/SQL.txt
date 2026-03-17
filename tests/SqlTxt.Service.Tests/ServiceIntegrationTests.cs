using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SqlTxt.Service;
using Xunit;

namespace SqlTxt.Service.Tests;

public class ServiceIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ServiceIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SqlTxt:DatabasePath", Path.GetTempPath());
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Exec_Insert_ReturnsRowsAffected()
    {
        var dbName = "SqlTxtSvc_" + Guid.NewGuid().ToString("N")[..8];
        var dbPath = Path.Combine(Path.GetTempPath(), dbName);

        try
        {
            await _client.PostAsJsonAsync("/exec", new { Sql = $"CREATE DATABASE {dbName}", DatabasePath = Path.GetTempPath() });
            await _client.PostAsJsonAsync("/exec", new { Sql = "CREATE TABLE T (Id CHAR(5) PRIMARY KEY)", DatabasePath = dbPath });
            var insertResp = await _client.PostAsJsonAsync("/exec", new { Sql = "INSERT INTO T (Id) VALUES ('1')", DatabasePath = dbPath });
            insertResp.EnsureSuccessStatusCode();
            var result = await insertResp.Content.ReadFromJsonAsync<ExecResponse>();
            Assert.NotNull(result);
            Assert.Equal(1, result!.RowsAffected);
        }
        finally
        {
            if (Directory.Exists(dbPath))
                Directory.Delete(dbPath, recursive: true);
        }
    }

    [Fact]
    public async Task Query_Select_ReturnsRows()
    {
        var dbName = "SqlTxtSvc_" + Guid.NewGuid().ToString("N")[..8];
        var dbPath = Path.Combine(Path.GetTempPath(), dbName);

        try
        {
            await _client.PostAsJsonAsync("/exec", new { Sql = $"CREATE DATABASE {dbName}", DatabasePath = Path.GetTempPath() });
            await _client.PostAsJsonAsync("/exec", new { Sql = "CREATE TABLE T (Id CHAR(5), Name CHAR(10))", DatabasePath = dbPath });
            await _client.PostAsJsonAsync("/exec", new { Sql = "INSERT INTO T (Id, Name) VALUES ('1', 'Alice')", DatabasePath = dbPath });

            var queryResp = await _client.PostAsJsonAsync("/query", new { Sql = "SELECT * FROM T", DatabasePath = dbPath });
            queryResp.EnsureSuccessStatusCode();
            var json = await queryResp.Content.ReadAsStringAsync();
            Assert.Contains("Alice", json);
            Assert.Contains("Id", json);
            Assert.Contains("Name", json);
        }
        finally
        {
            if (Directory.Exists(dbPath))
                Directory.Delete(dbPath, recursive: true);
        }
    }

    private record ExecResponse(int RowsAffected, IReadOnlyList<string>? Warnings);
}
