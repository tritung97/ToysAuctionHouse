namespace ToysAuctionHouse.Api;

public sealed record ApiResponse<T>(bool Success, T? Data, string? ErrorCode, string? Message)
{
    public static ApiResponse<T> Ok(T data) => new(true, data, null, null);

    public static ApiResponse<T> Error(string code, string message) => new(false, default, code, message);
}

public sealed record ApiResponse(bool Success, object? Data, string? ErrorCode, string? Message)
{
    public static ApiResponse Ok(object? data = null) => new(true, data, null, null);

    public static ApiResponse Error(string code, string message) => new(false, null, code, message);
}
