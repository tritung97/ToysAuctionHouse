using Microsoft.EntityFrameworkCore;
using ToysAuctionHouse.Application.Abstractions.Repositories;
using ToysAuctionHouse.Domain.Entities;
using ToysAuctionHouse.Domain.Enums;
using ToysAuctionHouse.Infrastructure.Persistence;

namespace ToysAuctionHouse.Infrastructure.Persistence.Repositories;

public sealed class SqlUserRepository : ISqlUserRepository
{
    private readonly AuctionDbContext _db;

    public SqlUserRepository(AuctionDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
        => _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);

    public async Task<IReadOnlyDictionary<Guid, User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<Guid, User>();
        }

        var users = await _db.Users.AsNoTracking()
            .Where(x => idList.Contains(x.Id))
            .ToListAsync(ct).ConfigureAwait(false);

        return users.ToDictionary(x => x.Id);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email, ct);

    public Task<User?> GetByOAuthAsync(AuthProvider provider, string providerKey, CancellationToken ct = default)
        => _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.AuthProvider == provider && x.ProviderKey == providerKey, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => _db.Users.AsNoTracking().AnyAsync(x => x.Email == email, ct);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
        => _db.Users.AsNoTracking().AnyAsync(x => x.Username == username, ct);

    public async Task<User> CreateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return user;
    }

    public async Task<User> UpdateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return user;
    }

    public async Task UpdatePhoneAsync(Guid userId, string phoneNumber, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct).ConfigureAwait(false);
        if (user is not null)
        {
            user.PhoneNumber = phoneNumber;
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
