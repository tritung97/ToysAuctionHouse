namespace ToysAuctionHouse.Application.Dtos;

public sealed record ProductCardDto(
    Guid Id,
    string Name,
    string Category,
    string Brand,
    int Condition,
    decimal CurrentPrice,
    DateTime? EndTime,
    int Status,
    string? ImageUrl);

public sealed record ProductDetailDto(
    Guid Id,
    Guid SellerId,
    string SellerName,
    string SellerPhone,
    string Name,
    string Category,
    string? Scale,
    string Brand,
    int Condition,
    string Description,
    IReadOnlyList<string> ImageUrls,
    int? AuctionStatus,
    DateTime? EndTime);
