using Microsoft.EntityFrameworkCore;
using PaymentSimulator.Features.Payments.Models;

namespace PaymentSimulator.Data;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options)
    : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(payment => payment.Id);
            entity.HasIndex(payment => payment.BookingId).IsUnique();
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
            entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}
