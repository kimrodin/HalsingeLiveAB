using Microsoft.EntityFrameworkCore;
using EventCatalog.Features.Events.Models;
using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Data;

public class EventCatalogDbContext : DbContext
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueSeatingMap> VenueSeatingMaps => Set<VenueSeatingMap>();

    public EventCatalogDbContext(
        DbContextOptions<EventCatalogDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(
    ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        #region EventDbModel
        modelBuilder.Entity<Event>()
            .HasKey(e => e.EventId);

        modelBuilder.Entity<Event>()
            .Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        modelBuilder.Entity<Event>()
            .Property(e => e.Price)
            .IsRequired();

        modelBuilder.Entity<Event>()
            .Property(e => e.Description)
            .HasMaxLength(2000);

        modelBuilder.Entity<Event>()
            .Property(e => e.StartDate)
            .IsRequired();

        modelBuilder.Entity<Event>()
            .Property(e => e.EndDate)
            .IsRequired();

        modelBuilder.Entity<Event>()
            .Property(e => e.OnSaleFrom)
            .IsRequired();

        modelBuilder.Entity<Event>()
            .HasOne(e => e.Venue)
            .WithMany(v => v.Events)
            .HasForeignKey(e => e.VenueId);
        #endregion

        modelBuilder.Entity<VenueSeatingMap>()
            .HasOne<Venue>()
            .WithMany(v => v.SeatingMaps)
            .HasForeignKey(m => m.VenueId);

        modelBuilder.Entity<VenueSeatingMap>()
            .Property(m => m.Name)
            .IsRequired();

        modelBuilder.Entity<VenueSeatingMap>()
            .Property(m => m.FileName)
            .IsRequired();

        modelBuilder.Entity<VenueSeatingMap>()
            .Property(m => m.StorageKey)
            .IsRequired();

        modelBuilder.Entity<VenueSeatingMap>()
            .Property(m => m.ContentType)
            .IsRequired();

    }
}
