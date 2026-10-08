using Microsoft.EntityFrameworkCore;
using EventCatalog.Features.Events.Models;

namespace EventCatalog.Data;

public class EventCatalogDbContext : DbContext
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Venue> Venues => Set<Venue>();

    public EventCatalogDbContext(
        DbContextOptions<EventCatalogDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(
    ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        #region Event
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

    }
}