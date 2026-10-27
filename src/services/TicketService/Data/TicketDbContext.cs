using Microsoft.EntityFrameworkCore;
using TicketService.Features.Tickets.Models;

namespace TicketService.Data;

public sealed class TicketDbContext(DbContextOptions<TicketDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets", table =>
            {
                table.HasCheckConstraint(
                    "CK_Tickets_PositiveTicketNumber",
                    "\"TicketNumberInBookingItem\" > 0");
                table.HasCheckConstraint(
                    "CK_Tickets_NonNegativeUnitPrice",
                    "\"UnitPrice\" >= 0");
            });

            entity.HasKey(ticket => ticket.TicketId);

            entity.Property(ticket => ticket.EventName)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(ticket => ticket.VenueName)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(ticket => ticket.SectionName)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(ticket => ticket.UnitPrice)
                .HasPrecision(12, 2);
            entity.Property(ticket => ticket.Currency)
                .HasMaxLength(3)
                .IsRequired();
            entity.Property(ticket => ticket.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.HasIndex(ticket => new
                {
                    ticket.BookingItemId,
                    ticket.TicketNumberInBookingItem
                })
                .IsUnique();
            entity.HasIndex(ticket => ticket.BookingId);
            entity.HasIndex(ticket => ticket.EventId);
        });
    }
}
