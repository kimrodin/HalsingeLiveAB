using Microsoft.EntityFrameworkCore;

public class EventCatalogDbContext : DbContext
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Venue> Venues => Set<Venue>();

    public EventCatalogDbContext(
        DbContextOptions<EventCatalogDbContext> options)
        : base(options)
    {
    }
}