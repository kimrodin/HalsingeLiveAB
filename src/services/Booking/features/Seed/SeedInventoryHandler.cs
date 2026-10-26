using Booking.Data;
using Microsoft.EntityFrameworkCore;
using InventoryModel = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.Features.Seed;

public sealed class SeedInventoryHandler(BookingDbContext dbContext)
{
    public async Task<SeedInventoryResponse> Handle(CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var eventIds = SeedInventoryData.Definitions.Select(item => item.EventId).Distinct().ToArray();
        var existing = await dbContext.EventInventory
            .Where(item => eventIds.Contains(item.EventId))
            .ToListAsync(cancellationToken);
        var createdCount = 0;

        foreach (var definition in SeedInventoryData.Definitions)
        {
            var inventory = existing.SingleOrDefault(item =>
                item.EventId == definition.EventId && item.SectionId == definition.SectionId);

            if (inventory is not null)
            {
                if (inventory.TotalQuantity != definition.TotalQuantity
                    || inventory.AvailableQuantity != definition.TotalQuantity
                    || inventory.Price != definition.Price)
                {
                    throw new SeedInventoryConflictException(
                        $"Existing mock inventory for event {definition.EventId} and section {definition.SectionId} differs from the seed definition.");
                }

                continue;
            }

            dbContext.EventInventory.Add(new InventoryModel
            {
                InventoryId = Guid.NewGuid(),
                EventId = definition.EventId,
                SectionId = definition.SectionId,
                TotalQuantity = definition.TotalQuantity,
                AvailableQuantity = definition.TotalQuantity,
                Price = definition.Price
            });
            createdCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SeedInventoryResponse(
            createdCount,
            SeedInventoryData.Definitions.Select(definition => new SeededInventoryResponse(
                definition.EventName,
                definition.SectionName,
                definition.EventId,
                definition.SectionId,
                definition.TotalQuantity,
                definition.Price)).ToArray());
    }

}

internal static class SeedInventoryData
{
    public static readonly SeedInventoryDefinition[] Definitions =
    [
        new(
            "Vinterkonsert i stora salen",
            "Sektion 1",
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            40,
            495m),
        new(
            "Vinterkonsert i stora salen",
            "Sektion 2",
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            60,
            395m),
        new(
            "Vinterkonsert i stora salen",
            "Sektion 3",
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000003"),
            80,
            295m),
        new(
            "Standup p\u00E5 Kulturhuset",
            "Sektion 1",
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            50,
            350m),
        new(
            "Standup p\u00E5 Kulturhuset",
            "Sektion 2",
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            50,
            300m),
        new(
            "Standup p\u00E5 Kulturhuset",
            "Sektion 3",
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Guid.Parse("20000000-0000-0000-0000-000000000003"),
            50,
            250m),
        new(
            "V\u00E5rkonsert i stora salen",
            "Sektion 1",
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            75,
            450m),
        new(
            "V\u00E5rkonsert i stora salen",
            "Sektion 2",
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            75,
            375m),
        new(
            "V\u00E5rkonsert i stora salen",
            "Sektion 3",
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Guid.Parse("20000000-0000-0000-0000-000000000003"),
            75,
            325m),
        new(
            "Danskv\u00E4ll i stora salen",
            "Sektion 1",
            Guid.Parse("10000000-0000-0000-0000-000000000004"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            75,
            395m),
        new(
            "Danskv\u00E4ll i stora salen",
            "Sektion 2",
            Guid.Parse("10000000-0000-0000-0000-000000000004"),
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            75,
            345m),
        new(
            "Danskv\u00E4ll i stora salen",
            "Sektion 3",
            Guid.Parse("10000000-0000-0000-0000-000000000004"),
            Guid.Parse("20000000-0000-0000-0000-000000000003"),
            75,
            295m),
        new(
            "Akustisk kv\u00E4ll p\u00E5 Studion",
            "St\u00E5plats",
            Guid.Parse("10000000-0000-0000-0000-000000000005"),
            Guid.Parse("20000000-0000-0000-0000-000000000004"),
            120,
            225m),
        new(
            "Indie Live p\u00E5 Studion",
            "St\u00E5plats",
            Guid.Parse("10000000-0000-0000-0000-000000000006"),
            Guid.Parse("20000000-0000-0000-0000-000000000004"),
            120,
            275m),
        new(
            "Sommarklubb p\u00E5 Studion",
            "St\u00E5plats",
            Guid.Parse("10000000-0000-0000-0000-000000000007"),
            Guid.Parse("20000000-0000-0000-0000-000000000004"),
            120,
            250m),
        new(
            "H\u00E4lsingesommar Festival",
            "St\u00E5plats",
            Guid.Parse("10000000-0000-0000-0000-000000000008"),
            Guid.Parse("20000000-0000-0000-0000-000000000005"),
            600,
            495m),
        new(
            "Rock under bar himmel",
            "St\u00E5plats",
            Guid.Parse("10000000-0000-0000-0000-000000000009"),
            Guid.Parse("20000000-0000-0000-0000-000000000005"),
            600,
            595m),
        new(
            "Avslutningsfestivalen",
            "St\u00E5plats",
            Guid.Parse("10000000-0000-0000-0000-000000000010"),
            Guid.Parse("20000000-0000-0000-0000-000000000005"),
            600,
            395m),
    ];
}

internal sealed record SeedInventoryDefinition(
    string EventName,
    string SectionName,
    Guid EventId,
    Guid SectionId,
    int TotalQuantity,
    decimal Price);

public sealed record SeedInventoryResponse(
    int CreatedCount,
    IReadOnlyList<SeededInventoryResponse> Inventories);

public sealed record SeededInventoryResponse(
    string EventName,
    string SectionName,
    Guid EventId,
    Guid SectionId,
    int TotalQuantity,
    decimal Price);

public sealed class SeedInventoryConflictException(string message) : InvalidOperationException(message);
