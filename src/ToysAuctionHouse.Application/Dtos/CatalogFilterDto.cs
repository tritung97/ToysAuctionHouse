namespace ToysAuctionHouse.Application.Dtos;

public sealed class CatalogFilterDto
{
    public string? Category { get; set; }
    public int? Condition { get; set; }
    public int? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
