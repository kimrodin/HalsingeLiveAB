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
            "Mock concert",
            "Front section",
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            40,
            75m),
        new(
            "Mock concert",
            "Rear section",
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            60,
            45m),
        new(
            "Mock theater",
            "Main section",
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Guid.Parse("20000000-0000-0000-0000-000000000003"),
            80,
            30m)
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
