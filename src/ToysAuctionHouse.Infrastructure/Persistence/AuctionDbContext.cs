using Microsoft.EntityFrameworkCore;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Infrastructure.Persistence.Configurations;

namespace ToysAuctionHouse.Infrastructure.Persistence;

public class AuctionDbContext : DbContext
{
    public AuctionDbContext(DbContextOptions<AuctionDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<ToyProduct> ToyProducts => Set<ToyProduct>();
    public DbSet<AuctionSession> AuctionSessions => Set<AuctionSession>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuctionDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
