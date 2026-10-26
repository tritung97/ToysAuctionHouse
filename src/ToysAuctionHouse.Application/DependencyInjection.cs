using Microsoft.Extensions.DependencyInjection;
using ToysAuctionHouse.Application.Services;

namespace ToysAuctionHouse.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<AuctionService>();
        services.AddScoped<OrderService>();

        return services;
    }
}
