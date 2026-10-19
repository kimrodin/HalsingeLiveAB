using Booking.Features.Bookings.Models;
using Microsoft.EntityFrameworkCore;
using BookingModel = Booking.Features.Bookings.Models.Booking;
using InventoryModel = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.Data;

public class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<BookingModel> Bookings => Set<BookingModel>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<InventoryModel> EventInventory => Set<InventoryModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookingModel>(entity =>
        {
            entity.ToTable("Bookings");
            entity.HasKey(booking => booking.BookingId);
            entity.Property(booking => booking.CustomerId).IsRequired();
            entity.Property(booking => booking.Status).IsRequired();
            entity.Property(booking => booking.TotalPrice).HasPrecision(12, 2);
            entity.HasMany(booking => booking.Items)
                .WithOne(item => item.Booking)
                .HasForeignKey(item => item.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BookingItem>(entity =>
        {
            entity.ToTable("BookingItems");
            entity.HasKey(item => item.BookingItemId);
            entity.Property(item => item.UnitPrice).HasPrecision(12, 2);
        });

        modelBuilder.Entity<InventoryModel>(entity =>
        {
            entity.ToTable("EventInventory");
            entity.HasKey(inventory => inventory.InventoryId);
            entity.Property(inventory => inventory.TicketType).IsRequired().HasMaxLength(255);
            entity.Property(inventory => inventory.Price).HasPrecision(12, 2);
            entity.HasIndex(inventory => inventory.EventId);
            entity.HasMany(inventory => inventory.BookingItems)
                .WithOne(item => item.EventInventory)
                .HasForeignKey(item => item.InventoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
