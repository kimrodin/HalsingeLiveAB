using EventCatalog.Data;
using EventCatalog.Features.Venues.Models;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Venues.UpdateVenue;

public class UpdateVenueHandler(EventCatalogDbContext dbContext)
{
    public async Task<bool> Handle(UpdateVenueRequest request, CancellationToken cancellationToken = default)
    {
        VenueValidation.ValidateId(request.VenueId, nameof(request.VenueId));
        VenueValidation.ValidateVenue(request.Name, request.StreetAddress, request.PostalCode, request.City, request.Capacity);
        if (request.Sections is null)
            throw new VenueValidationException(new Dictionary<string, string[]> { ["Sections"] = ["Sections are required."] });
        foreach (var section in request.Sections)
            VenueValidation.ValidateSection(section.Name, section.Capacity, section.SeatingType);
        if (request.Sections.Where(section => section.SectionId.HasValue).Select(section => section.SectionId).Distinct().Count()
            != request.Sections.Count(section => section.SectionId.HasValue))
            throw new VenueValidationException(new Dictionary<string, string[]> { ["Sections"] = ["A section cannot appear more than once."] });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var venue = await VenueCapacityRules.LockVenueAsync(dbContext, request.VenueId, cancellationToken);

        if (venue is null)
            return false;

        var sections = await dbContext.Sections
            .FromSqlInterpolated($"SELECT * FROM \"Sections\" WHERE \"VenueId\" = {request.VenueId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var currentById = sections.ToDictionary(section => section.SectionId);
        var requestedIds = request.Sections.Where(section => section.SectionId.HasValue)
            .Select(section => section.SectionId!.Value).ToHashSet();
        if (requestedIds.Any(id => !currentById.ContainsKey(id)))
            return false;

        var deleted = sections.Where(section => !requestedIds.Contains(section.SectionId)).ToArray();
        var changed = request.Sections.Where(section => section.SectionId.HasValue)
            .Select(section => (Request: section, Current: currentById[section.SectionId!.Value]))
            .Where(item => item.Current.SectionCapacity != item.Request.Capacity
                || item.Current.SeatingType != item.Request.SeatingType).ToArray();
        var protectedIds = deleted.Select(section => section.SectionId)
            .Concat(changed.Select(item => item.Current.SectionId)).ToArray();
        if (protectedIds.Length > 0 && await dbContext.EventSectionPrice
            .AnyAsync(price => protectedIds.Contains(price.SectionId), cancellationToken))
            throw new VenueCapacityConflictException("Sections used by events cannot be removed or have capacity or seating type changed.");

        foreach (var section in deleted)
        {
            if (await dbContext.Seats.AnyAsync(seat => seat.SectionId == section.SectionId, cancellationToken))
                throw new VenueCapacityConflictException("A section with seats cannot be removed.");
        }
        foreach (var item in changed)
        {
            var seatCount = await dbContext.Seats.CountAsync(seat => seat.SectionId == item.Current.SectionId, cancellationToken);
            if (item.Request.Capacity < seatCount || (item.Current.SeatingType != item.Request.SeatingType && seatCount > 0))
                throw new VenueCapacityConflictException("Section capacity cannot be lower than its seat count, and seating type cannot change while seats exist.");
        }
        if (request.Capacity < request.Sections.Sum(section => (long)section.Capacity))
            throw new VenueCapacityConflictException(
                "The venue capacity cannot be less than the combined section capacity.");

        dbContext.Sections.RemoveRange(deleted);
        foreach (var sectionRequest in request.Sections)
        {
            if (sectionRequest.SectionId is Guid sectionId)
            {
                var section = currentById[sectionId];
                section.Name = sectionRequest.Name;
                section.SectionCapacity = sectionRequest.Capacity;
                section.SeatingType = sectionRequest.SeatingType;
            }
            else
            {
                dbContext.Sections.Add(new Section
                {
                    SectionId = Guid.NewGuid(), VenueId = request.VenueId,
                    Name = sectionRequest.Name, SectionCapacity = sectionRequest.Capacity,
                    SeatingType = sectionRequest.SeatingType
                });
            }
        }

        venue.VenueName = request.Name;
        venue.VenueStreetAddress = request.StreetAddress;
        venue.VenuePostalCode = request.PostalCode;
        venue.VenueCity = request.City;
        venue.VenueCapacity = request.Capacity;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
