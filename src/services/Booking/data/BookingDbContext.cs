using Booking.Features.Bookings.Models;
using Booking.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using BookingModel = Booking.Features.Bookings.Models.Booking;
using InventoryModel = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.Data;

public class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<BookingModel> Bookings => Set<BookingModel>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<InventoryModel> EventInventory => Set<InventoryModel>();
    public DbSet<TicketIssueOutboxMessage> TicketIssueOutboxMessages => Set<TicketIssueOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookingModel>(entity =>
        {
            entity.ToTable("Bookings");
            entity.HasKey(booking => booking.BookingId);
            entity.Property(booking => booking.CustomerId).IsRequired(false);
            entity.Property(booking => booking.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(booking => booking.CustomerEmail).HasMaxLength(254).IsRequired();
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
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            entity.Property(item => item.EventName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.VenueName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SectionName).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<InventoryModel>(entity =>
        {
            entity.ToTable("EventInventory");
            entity.HasKey(inventory => inventory.InventoryId);
            entity.Property(inventory => inventory.AvailableQuantity).IsRequired();
            entity.Property(inventory => inventory.Price).HasPrecision(12, 2);
            entity.HasIndex(inventory => inventory.EventId);
            entity.HasIndex(inventory => new { inventory.EventId, inventory.SectionId }).IsUnique();
            entity.HasMany(inventory => inventory.BookingItems)
                .WithOne(item => item.EventInventory)
                .HasForeignKey(item => item.InventoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TicketIssueOutboxMessage>(entity =>
        {
            entity.ToTable("TicketIssueOutboxMessages");
            entity.HasKey(message => message.MessageId);
            entity.Property(message => message.EventName).HasMaxLength(200).IsRequired();
            entity.Property(message => message.VenueName).HasMaxLength(200).IsRequired();
            entity.Property(message => message.SectionName).HasMaxLength(100).IsRequired();
            entity.Property(message => message.Currency).HasMaxLength(3).IsRequired();
            entity.Property(message => message.UnitPrice).HasPrecision(12, 2);
            entity.Property(message => message.LastError).HasMaxLength(2000).IsRequired(false);
            entity.HasIndex(message => message.BookingItemId).IsUnique();
            entity.HasIndex(message => new { message.DeliveredAtUtc, message.NextAttemptAtUtc });
        });
    }
}
