using Microsoft.EntityFrameworkCore;
using PaymentSimulator.Features.Payments.Models;
using PaymentSimulator.Features.Payments.Simulation;

namespace PaymentSimulator.Data;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options)
    : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SimulationSettings> SimulationSettings => Set<SimulationSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SimulationSettings>(entity =>
        {
            entity.HasKey(settings => settings.Id);
            entity.Property(settings => settings.Id).ValueGeneratedNever();
            entity.Property(settings => settings.Outcome).HasConversion<string>().HasMaxLength(20);
            entity.ToTable("SimulationSettings", table =>
                table.HasCheckConstraint("CK_SimulationSettings_Valid", "\"Id\" = 1 AND \"Outcome\" IN ('Succeeded', 'Failed', 'TimedOut', 'Random') AND \"DelayMs\" >= 0 AND \"TimeoutMs\" BETWEEN 1 AND 30000"));
        });
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(payment => payment.Id);
            entity.HasIndex(payment => payment.BookingId).IsUnique();
            entity.HasIndex(payment => new { payment.PaidAt, payment.Id })
                .HasFilter("\"PaidAt\" IS NOT NULL");
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
            entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(payment => payment.PlannedOutcome).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(payment => new { payment.Status, payment.CompleteAfter });
        });
    }
}
