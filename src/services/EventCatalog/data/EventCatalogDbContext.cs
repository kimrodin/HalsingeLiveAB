using Microsoft.EntityFrameworkCore;
using EventCatalog.Features.Events.Models;
using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Data;

public class EventCatalogDbContext : DbContext
{
    public DbSet<Event> Events => Set<Event>();
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
        // Kör basklassens modellkonfiguration innan våra egna regler läggs till.
        base.OnModelCreating(modelBuilder);

        // Reglerna beskriver databasschemat och appliceras genom migrationer.
        // IsRequired förbjuder NULL; tomma strängar och affärsregler valideras separat.
        #region EventDbModel
        // EventId är primärnyckeln som identifierar varje evenemang unikt.
        modelBuilder.Entity<Event>()
            .HasKey(e => e.EventId);

        // Evenemangets namn får inte vara NULL och får innehålla högst 200 tecken.
        modelBuilder.Entity<Event>()
            .Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        // Priset får inte vara NULL. Regeln kontrollerar inte att priset är positivt.
        modelBuilder.Entity<Event>()
            .Property(e => e.Price)
            .IsRequired();

        // Beskrivningen får innehålla högst 2 000 tecken.
        modelBuilder.Entity<Event>()
            .Property(e => e.Description)
            .HasMaxLength(2000);

        // Startdatum får inte vara NULL.
        modelBuilder.Entity<Event>()
            .Property(e => e.StartDate)
            .IsRequired();

        // Slutdatum får inte vara NULL. Ordningen mellan start och slut valideras separat.
        modelBuilder.Entity<Event>()
            .Property(e => e.EndDate)
            .IsRequired();

        // Datumet då biljettförsäljningen börjar får inte vara NULL.
        modelBuilder.Entity<Event>()
            .Property(e => e.OnSaleFrom)
            .IsRequired();

        // Ett evenemang hör till en lokal, som kan ha flera evenemang.
        // VenueId är främmande nyckel och pekar på lokalens primärnyckel.
        modelBuilder.Entity<Event>()
            .HasOne(e => e.Venue)
            .WithMany(v => v.Events)
            .HasForeignKey(e => e.VenueId);
        #endregion

        // En platskarta hör till en lokal via VenueId; lokalen kan ha flera platskartor.
        // HasOne<Venue>() fungerar även utan en Venue-navigation i platskartans modell.
        modelBuilder.Entity<VenueSeatingMap>()
            .HasOne<Venue>()
            .WithMany(v => v.SeatingMaps)
            .HasForeignKey(m => m.VenueId);

        // Platskartans visningsnamn får inte vara NULL.
        modelBuilder.Entity<VenueSeatingMap>()
            .Property(m => m.Name)
            .IsRequired();

        // Filnamnet får inte vara NULL. Själva filen lagras inte av detta fält.
        modelBuilder.Entity<VenueSeatingMap>()
            .Property(m => m.FileName)
            .IsRequired();

        // Nyckeln som används för att hitta filen i fillagringen får inte vara NULL.
        modelBuilder.Entity<VenueSeatingMap>()
            .Property(m => m.StorageKey)
            .IsRequired();

        // Filens MIME-typ, exempelvis application/pdf, får inte vara NULL.
        modelBuilder.Entity<VenueSeatingMap>()
            .Property(m => m.ContentType)
            .IsRequired();

        // En sektion hör till en lokal via VenueId; lokalen kan ha flera sektioner.
        // Cascade innebär att lokalens sektioner raderas när lokalen raderas.
        modelBuilder.Entity<Section>()
            .HasOne<Venue>()
            .WithMany(v => v.VenueSections)
            .HasForeignKey(s => s.VenueId)
            .OnDelete(DeleteBehavior.Cascade);

        // Sektionens namn får inte vara NULL och får innehålla högst 100 tecken.
        modelBuilder.Entity<Section>()
            .Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        // En stol hör till en sektion via SectionId; sektionen kan ha flera stolar.
        // Cascade raderar stolarna när sektionen raderas, även när lokalen tas bort.
        modelBuilder.Entity<Seat>()
            .HasOne<Section>()
            .WithMany(s => s.Seats)
            .HasForeignKey(s => s.SectionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Radens beteckning, exempelvis A, får inte vara NULL och får ha högst 20 tecken.
        modelBuilder.Entity<Seat>()
            .Property(s => s.Row)
            .IsRequired()
            .HasMaxLength(20);

        // Samma stolnummer får förekomma på olika rader,
        // men bara en gång per rad inom sektionen.
        modelBuilder.Entity<Seat>()
            .HasIndex(s => new { s.SectionId, s.Row, s.Number })
            .IsUnique();

        // Lokalens namn får inte vara NULL och får innehålla högst 200 tecken.
        modelBuilder.Entity<Venue>()
            .Property(v => v.VenueName)
            .IsRequired()
            .HasMaxLength(200);

        // Lokalens adress får inte vara NULL och får innehålla högst 500 tecken.
        modelBuilder.Entity<Venue>()
            .Property(v => v.VenueAddress)
            .IsRequired()
            .HasMaxLength(500);
    }
}
