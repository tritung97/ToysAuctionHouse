using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;

namespace ToysAuctionHouse.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task SeedAsync(AuctionDbContext db, CancellationToken ct = default)
    {
        if (db.Users.Any())
        {
            return;
        }

        var seller = new User
        {
            Username = "seller_demo",
            Email = "seller@example.com",
            PasswordHash = "$2a$11$placeholder.do.not.use.in.prod.xxxxxxxxxxxxxxxxxxxxxx",
            FullName = "Demo Seller",
            PhoneNumber = "+84000000001",
            AuthProvider = AuthProvider.Local,
        };

        var buyer = new User
        {
            Username = "buyer_demo",
            Email = "buyer@example.com",
            PasswordHash = "$2a$11$placeholder.do.not.use.in.prod.xxxxxxxxxxxxxxxxxxxxxx",
            FullName = "Demo Buyer",
            PhoneNumber = "+84000000002",
            AuthProvider = AuthProvider.Local,
        };

        db.Users.AddRange(seller, buyer);
        await db.SaveChangesAsync(ct);

        var now = DateTime.UtcNow;

        var gundam = new ToyProduct
        {
            SellerId = seller.Id,
            Name = "MG Barbatos Lupus",
            Category = "Gunpla",
            Scale = "1/100",
            Brand = "Bandai",
            Condition = ToyCondition.NewSeal,
            Description = "Factory-sealed Master Grade, no box damage.",
            ImageUrls = new List<string> { "https://img.example.com/barbatos-1.jpg" },
        };

        var hottoys = new ToyProduct
        {
            SellerId = seller.Id,
            Name = "Hot Toys Iron Man Mk. 42",
            Category = "HotToys",
            Scale = "1/6",
            Brand = "Hot Toys",
            Condition = ToyCondition.SecondHand,
            Description = "Opened, displayed, complete with all accessories.",
            ImageUrls = new List<string> { "https://img.example.com/ironman-1.jpg" },
        };

        db.ToyProducts.AddRange(gundam, hottoys);
        await db.SaveChangesAsync(ct);

        // One scheduled (starts in +1h) and one live (started now, ends +1h).
        db.AuctionSessions.AddRange(
            new AuctionSession
            {
                ProductId = gundam.Id,
                StartPrice = 50,
                StepPrice = 5,
                CurrentPrice = 50,
                StartTime = now.AddHours(1),
                EndTime = now.AddHours(3),
                Status = AuctionSessionStatus.Scheduled,
                IsAntiSnipingEnabled = true,
            },
            new AuctionSession
            {
                ProductId = hottoys.Id,
                StartPrice = 200,
                StepPrice = 10,
                CurrentPrice = 200,
                StartTime = now.AddMinutes(-5),
                EndTime = now.AddHours(1),
                Status = AuctionSessionStatus.Active,
                IsAntiSnipingEnabled = true,
            });

        await db.SaveChangesAsync(ct);
    }
}
