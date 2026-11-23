using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderFlow.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public NpgsqlDataSource Orders { get; private set; } = null!;
    public NpgsqlDataSource Inventory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Orders = await CreateDatabaseAsync("orders", "orders.sql");
        Inventory = await CreateDatabaseAsync("inventory", "inventory.sql");
    }

    public async Task DisposeAsync()
    {
        await Orders.DisposeAsync();
        await Inventory.DisposeAsync();
        await _container.DisposeAsync();
    }

    public async Task<T> ScalarAsync<T>(NpgsqlDataSource dataSource, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = dataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private async Task<NpgsqlDataSource> CreateDatabaseAsync(string database, string schemaFile)
    {
        await using (var admin = NpgsqlDataSource.Create(_container.GetConnectionString()))
        await using (var create = admin.CreateCommand($"create database {database}"))
            await create.ExecuteNonQueryAsync();

        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database };
        var dataSource = NpgsqlDataSource.Create(connectionString.ConnectionString);
        var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "database", schemaFile));
        await using var command = dataSource.CreateCommand(schema);
        await command.ExecuteNonQueryAsync();
        return dataSource;
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
