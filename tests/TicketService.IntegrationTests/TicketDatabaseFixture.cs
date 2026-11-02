using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using TicketService.Data;

namespace TicketService.IntegrationTests;

public sealed class TicketIntegrationTheoryAttribute : TheoryAttribute
{
    public TicketIntegrationTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TICKET_TEST_POSTGRES")))
            Skip = "Set TICKET_TEST_POSTGRES to run against PostgreSQL.";
    }
}

public sealed class TicketDatabaseFixture : IAsyncLifetime
{
    private readonly string databaseName = $"ticket_tests_{Guid.NewGuid():N}";
    private string? adminConnectionString;
    private NpgsqlDataSource? dataSource;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("TICKET_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured))
            return;

        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
        await create.ExecuteNonQueryAsync();
        adminConnectionString = builder.ConnectionString;
        builder.Database = databaseName;
        builder.Pooling = true;
        dataSource = NpgsqlDataSource.Create(builder.ConnectionString);
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    public TicketDbContext CreateContext(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<TicketDbContext>()
            .UseNpgsql(dataSource ?? throw new InvalidOperationException("Test database is not configured."))
            .AddInterceptors(interceptors).Options);

    public async Task DisposeAsync()
    {
        if (dataSource is not null)
            await dataSource.DisposeAsync();
        if (adminConnectionString is null)
            return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        // Only the randomly named database created by this fixture is removed.
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

// Both requests finish their reads before either can save, forcing the database conflict paths.
internal sealed class ConcurrentSaveGate : SaveChangesInterceptor
{
    private int remaining = 2;
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Decrement(ref remaining) == 0)
            release.TrySetResult();
        await release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        return result;
    }
}
