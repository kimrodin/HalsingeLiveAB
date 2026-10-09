using Microsoft.EntityFrameworkCore;
using EventCatalog.Features.Events.Models;
using EventCatalog.Features.Venues.Models;
using System.Dynamic;

namespace EventCatalog.Data;

public class EventCatalogDbContext : DbContext
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventSectionPrice> EventSectionPrice => Set<EventSectionPrice>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueSeatingMap> VenueSeatingMaps => Set<VenueSeatingMap>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Section> Sections => Set<Section>();


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

        #region EventSectionPriceDbModel
        modelBuilder.Entity<EventSectionPrice>()
            .HasKey(p => new { p.EventId, p.SectionId });

        modelBuilder.Entity<EventSectionPrice>()
            .HasOne(p => p.Event)
            .WithMany(e => e.SectionPrices)
            .HasForeignKey(p => p.EventId);

        modelBuilder.Entity<EventSectionPrice>()
            .HasOne(p => p.Section)
            .WithMany(s => s.EventPrices)
            .HasForeignKey(p => p.SectionId);
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

        modelBuilder.Entity<Section>()
            .HasOne<Venue>()
            .WithMany(v => v.VenueSections)
            .HasForeignKey(s => s.VenueId);

        modelBuilder.Entity<Seat>()
            .HasOne<Section>()
            .WithMany(s => s.Seats)
            .HasForeignKey(s => s.SectionId);

        modelBuilder.Entity<Seat>()
            .HasIndex(s => new { s.SectionId, s.Row, s.Number })
            .IsUnique();

        modelBuilder.Entity<Seat>()
            .Property(s => s.Row)
            .IsRequired();

        modelBuilder.Entity<Section>()
            .Property(s => s.Name)
            .IsRequired();

    }
}
