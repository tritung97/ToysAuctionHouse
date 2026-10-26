namespace ToysAuctionHouse.Application.Dtos;

public sealed record CreateProductDto(
    string Name,
    string Category,
    string? Scale,
    string Brand,
    int Condition,
    string Description,
    IReadOnlyList<string> ImageUrls);

public sealed record CreateAuctionDto(
    decimal StartPrice,
    decimal StepPrice,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAntiSnipingEnabled);

public sealed record CreateAuctionRequest(CreateProductDto Product, CreateAuctionDto Auction);

public sealed record BidDto(Guid BidderId, string BidderName, decimal Amount, DateTime Timestamp);

public sealed record AuctionRoomDto(
    Guid AuctionId,
    Guid ProductId,
    string ProductName,
    string Category,
    string Brand,
    string? Scale,
    int Condition,
    string Description,
    IReadOnlyList<string> ImageUrls,
    Guid SellerId,
    string SellerName,
    string SellerPhone,
    decimal StartPrice,
    decimal StepPrice,
    decimal CurrentPrice,
    DateTime StartTime,
    DateTime EndTime,
    string Status,
    bool IsAntiSnipingEnabled,
    IReadOnlyList<BidDto> TopBids);

public sealed record BidResultDto(
    string Status,
    decimal CurrentPrice,
    Guid? WinnerId,
    bool Extended,
    DateTime? NewEndTime,
    string? Message);
