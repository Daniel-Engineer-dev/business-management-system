using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QTKD.Api.Data;

namespace QTKD.Tests.Integration;

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string connectionString;
    private readonly string adminConnectionString;
    private readonly string databaseName = $"qtkd_test_{Guid.NewGuid():N}";

    public ApiFixture()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("QTKD_TEST_ADMIN")
            ?? "Host=127.0.0.1;Port=55437;Database=postgres;Username=qtkd_test");
        adminConnectionString = connection.ConnectionString;
        connection.Database = databaseName;
        connectionString = connection.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:QTKD", connectionString);

    public QtkdDbContext OpenDatabase() => new(
        new DbContextOptionsBuilder<QtkdDbContext>().UseNpgsql(connectionString).Options);

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\" ENCODING 'UTF8' TEMPLATE template0", connection);
        await command.ExecuteNonQueryAsync();
        await using var db = OpenDatabase();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("PostgreSQL API")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;
