using Microsoft.EntityFrameworkCore;
using ToysAuctionHouse.Application.Abstractions;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;
using ToysAuctionHouse.Infrastructure.Persistence;
using ToysAuctionHouse.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ToysAuctionHouse.IntegrationTests;

/// <summary>
/// Verifies the SQL ledger only records one order when the auction finalizer runs
/// against the same auction more than once (idempotency via unique keys). Requires
/// a reachable SQL Server (docker-compose). Tagged Category=Integration.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FinalizePersistenceTests : IClassFixture<InfraFixture>
{
    private readonly InfraFixture _fixture;

    public FinalizePersistenceTests(InfraFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Finalize_is_idempotent_creating_single_order()
    {
        if (!_fixture.SqlAvailable()) return;
        if (_fixture.SqlOptions is null) return;

        var auctionId = _fixture.NextAuctionId();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        await using (var setup = new AuctionDbContext(_fixture.SqlOptions))
        {
            await setup.Database.EnsureCreatedAsync();
            await SeedAsync(setup, auctionId, sellerId, buyerId);
        }

        var repo = new SqlAuctionRepository(new AuctionDbContext(_fixture.SqlOptions));
        var command = new FinalizeCommand(
            auctionId,
            WinnerId: buyerId,
            FinalAmount: 500m,
            new List<BidPersistItem>
            {
                new(auctionId, buyerId, 500m, "test-finalize-key", DateTime.UtcNow),
            });

        var firstOrderId = await repo.FinalizeAndPersistAsync(command);
        Assert.NotNull(firstOrderId);

        // Re-finalization must be rejected by the unique index on AuctionSessionId → no second order.
        var reThrew = false;
        try
        {
            await repo.FinalizeAndPersistAsync(command);
        }
        catch (DbUpdateException)
        {
            reThrew = true;
        }
        Assert.True(reThrew, "Re-finalization of the same auction must not silently create a duplicate order.");

        await using var verify = new AuctionDbContext(_fixture.SqlOptions);
        var orderCount = await verify.Orders.CountAsync(o => o.AuctionSessionId == auctionId);
        var bidCount = await verify.Bids.CountAsync(b => b.AuctionSessionId == auctionId && b.IdempotencyKey == "test-finalize-key");
        var session = await verify.AuctionSessions.Include(s => s.Product).FirstOrDefaultAsync(s => s.Id == auctionId);

        Assert.Equal(1, orderCount);
        Assert.Equal(1, bidCount);
        Assert.NotNull(session);
        Assert.Equal(AuctionSessionStatus.Ended, session!.Status);

        // Cleanup to keep the test DB tidy for subsequent runs.
        await using var cleanup = new AuctionDbContext(_fixture.SqlOptions);
        await cleanup.Orders.Where(o => o.AuctionSessionId == auctionId).ExecuteDeleteAsync();
        await cleanup.Bids.Where(b => b.AuctionSessionId == auctionId).ExecuteDeleteAsync();
        await cleanup.AuctionSessions.Where(s => s.Id == auctionId).ExecuteDeleteAsync();
        await cleanup.ToyProducts.Where(p => p.SellerId == sellerId).ExecuteDeleteAsync();
        await cleanup.Users.Where(u => u.Id == sellerId || u.Id == buyerId).ExecuteDeleteAsync();
    }

    private static async Task SeedAsync(AuctionDbContext db, Guid auctionId, Guid sellerId, Guid buyerId)
    {
        var seller = new User
        {
            Id = sellerId,
            Username = "finalizer_seller",
            Email = "finalizer_seller@example.com",
            FullName = "Finalizer Seller",
            PhoneNumber = "+15551234567",
        };
        var buyer = new User
        {
            Id = buyerId,
            Username = "finalizer_buyer",
            Email = "finalizer_buyer@example.com",
            FullName = "Finalizer Buyer",
            PhoneNumber = "+15557654321",
        };
        db.Users.AddRange(seller, buyer);

        var product = new ToyProduct
        {
            SellerId = seller.Id,
            Name = "Finalizer Test Kit",
            Category = "Gunpla",
            Brand = "Bandai",
            Condition = ToyCondition.NewSeal,
            Description = "test",
        };
        db.ToyProducts.Add(product);
        await db.SaveChangesAsync();

        var session = new AuctionSession
        {
            Id = auctionId,
            ProductId = product.Id,
            StartPrice = 100m,
            StepPrice = 10m,
            CurrentPrice = 100m,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(-1),
            Status = AuctionSessionStatus.Active,
        };
        db.AuctionSessions.Add(session);
        await db.SaveChangesAsync();
    }
}
