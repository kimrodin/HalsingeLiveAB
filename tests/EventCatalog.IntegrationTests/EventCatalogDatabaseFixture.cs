using EventCatalog.Data;
using EventCatalog.Features.Events.CreateEvent;
using EventCatalog.Features.Venues.CreateSection;
using EventCatalog.Features.Venues.CreateVenue;
using EventCatalog.Features.Venues.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventCatalog.IntegrationTests;

// Fail if PostgreSQL is unavailable: a green run must mean these tests actually ran.
public sealed class EventCatalogDatabaseFixture : IAsyncLifetime
{
    private readonly string databaseName = "eventcatalog_tests_" + Guid.NewGuid().ToString("N");
    private string? connectionString;
    private string? adminConnectionString;
    private bool databaseCreated;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("EVENTCATALOG_TEST_POSTGRES")
            ?? "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=postgres";
        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false };
        adminConnectionString = builder.ConnectionString;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
        await create.ExecuteNonQueryAsync();
        databaseCreated = true;
        builder.Database = databaseName;
        connectionString = builder.ConnectionString;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public EventCatalogDbContext CreateContext() => new(
        new DbContextOptionsBuilder<EventCatalogDbContext>()
            .UseNpgsql(connectionString ?? throw new InvalidOperationException("Test database is not initialized."))
            .Options);

    public async Task<CreateVenueResponse> CreateVenueAsync(int capacity = 100)
    {
        await using var db = CreateContext();
        return await new CreateVenueHandler(db).Handle(new("Arena", "Main Street 1", "12345", "Hudiksvall", capacity));
    }

    public async Task<CreateSectionResponse> CreateSectionAsync(Guid venueId, int capacity = 40)
    {
        await using var db = CreateContext();
        return await new CreateSectionHandler(db).Handle(venueId, new("Floor", capacity, SeatingType.GeneralAdmission));
    }

    public static CreateEventRequest EventRequest(Guid venueId, Guid sectionId) => new(
        "Concert", "An evening of music", venueId,
        new DateTime(2030, 6, 1, 18, 0, 0, DateTimeKind.Utc),
        new DateTime(2030, 6, 1, 20, 0, 0, DateTimeKind.Utc),
        new DateTime(2030, 5, 1, 0, 0, 0, DateTimeKind.Utc), [new(sectionId, 125m)]);

    public async Task<Guid> CreateEventAsync(Guid venueId, Guid sectionId)
    {
        await using var db = CreateContext();
        return (await new CreateEventHandler(db).Handle(EventRequest(venueId, sectionId), CancellationToken.None)).EventId;
    }

    public async Task DisposeAsync()
    {
        if (!databaseCreated) return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}
