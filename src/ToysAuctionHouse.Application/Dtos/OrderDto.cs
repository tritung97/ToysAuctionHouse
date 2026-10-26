namespace ToysAuctionHouse.Application.Dtos;

public sealed record OrderDto(
    Guid Id,
    Guid AuctionSessionId,
    Guid BuyerId,
    string BuyerName,
    string BuyerPhone,
    Guid SellerId,
    string SellerName,
    string SellerPhone,
    decimal FinalAmount,
    int Status,
    string? Note,
    DateTime CreatedAt);
