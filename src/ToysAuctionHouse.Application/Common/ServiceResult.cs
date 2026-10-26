namespace ToysAuctionHouse.Application.Common;

public enum ServiceErrorCode
{
    InvalidInput,
    NotFound,
    Forbidden,
    Conflict,
    Unauthorized,
    PhoneRequired,
    CannotCancel,
    AuctionEnded,
    Outbid,
    Duplicate,
    NotStarted,
}

public sealed record ServiceResult
{
    public bool Success { get; init; }
    public ServiceErrorCode? ErrorCode { get; init; }
    public string? Message { get; init; }

    public static ServiceResult Ok => new() { Success = true };

    public static ServiceResult Fail(ServiceErrorCode code, string message)
        => new() { Success = false, ErrorCode = code, Message = message };
}

public sealed record ServiceResult<T>
{
    public bool Success { get; init; }
    public T? Value { get; init; }
    public ServiceErrorCode? ErrorCode { get; init; }
    public string? Message { get; init; }

    public static ServiceResult<T> Ok(T value) => new() { Success = true, Value = value };

    public static ServiceResult<T> Fail(ServiceErrorCode code, string message)
        => new() { Success = false, ErrorCode = code, Message = message };
}
