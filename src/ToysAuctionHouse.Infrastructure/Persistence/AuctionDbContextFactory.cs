using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ToysAuctionHouse.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> so migrations can be created and
/// applied without booting the full API host.
/// </summary>
public class AuctionDbContextFactory : IDesignTimeDbContextFactory<AuctionDbContext>
{
    public AuctionDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer")
            ?? "Server=localhost,1433;Database=ToysAuction;User Id=sa;Password=LocalDev!Passw0rd;Encrypt=False;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AuctionDbContext(options);
    }
}
