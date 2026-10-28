using Booking.Data;
using Booking.Features.Bookings;
using Booking.Features.EventInventory.EventCatalog;
using Microsoft.EntityFrameworkCore;
using InventoryModel = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.Features.EventInventory.CreateEventInventory;

public sealed class CreateEventInventoryFromCatalogHandler(
    BookingDbContext dbContext,
    IEventCatalogClient eventCatalogClient)
{
    public async Task<IReadOnlyList<CreateEventInventoryResponse>> Handle(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        BookingValidation.ValidateId(eventId, nameof(eventId));

        var catalogEvent = await eventCatalogClient.GetEventAsync(eventId, cancellationToken)
            ?? throw new EventCatalogNotFoundException(eventId);

        if (catalogEvent.SectionPrices.Count == 0)
            throw new EventCatalogContractException("The event has no priced sections to create inventory for.");

        var sectionIds = catalogEvent.SectionPrices.Select(section => section.SectionId).ToArray();
        if (sectionIds.Any(id => id == Guid.Empty)
            || sectionIds.Distinct().Count() != sectionIds.Length
            || catalogEvent.SectionPrices.Any(section => section.SectionCapacity <= 0))
            throw new EventCatalogContractException("EventCatalog returned invalid section inventory data.");

        foreach (var section in catalogEvent.SectionPrices)
        {
            try
            {
                BookingValidation.ValidatePriceAndTotal(section.Price, 1);
            }
            catch (BookingValidationException exception)
            {
                throw new EventCatalogContractException(
                    "EventCatalog returned an unsupported section price.", exception);
            }
        }

        var existingSectionIds = await dbContext.EventInventory
            .Where(inventory => inventory.EventId == eventId && sectionIds.Contains(inventory.SectionId))
            .Select(inventory => inventory.SectionId)
            .ToListAsync(cancellationToken);
        if (existingSectionIds.Count > 0)
            throw new InventoryConflictException("Inventory already exists for one or more sections of this event.");

        var inventories = catalogEvent.SectionPrices.Select(section => new InventoryModel
        {
            InventoryId = Guid.NewGuid(),
            EventId = eventId,
            SectionId = section.SectionId,
            TotalQuantity = section.SectionCapacity,
            AvailableQuantity = section.SectionCapacity,
            Price = section.Price
        }).ToArray();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.EventInventory.AddRange(inventories);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return inventories.Select(inventory => new CreateEventInventoryResponse(
            inventory.InventoryId,
            inventory.EventId,
            inventory.SectionId,
            inventory.TotalQuantity,
            inventory.AvailableQuantity,
            inventory.Price)).ToArray();
    }
}
