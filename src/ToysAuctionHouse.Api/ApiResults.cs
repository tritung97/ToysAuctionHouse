using Microsoft.AspNetCore.Mvc;
using ToysAuctionHouse.Application.Common;

namespace ToysAuctionHouse.Api;

public static class ApiResults
{
    public static IActionResult ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.Success && result.Value is not null)
        {
            return new OkObjectResult(ApiResponse<T>.Ok(result.Value));
        }

        return ToError(result.ErrorCode ?? ServiceErrorCode.InvalidInput, result.Message ?? "Request failed.");
    }

    public static IActionResult ToActionResult(ServiceResult result)
    {
        if (result.Success)
        {
            return new OkObjectResult(ApiResponse.Ok());
        }

        return ToError(result.ErrorCode ?? ServiceErrorCode.InvalidInput, result.Message ?? "Request failed.");
    }

    public static IActionResult ToError(ServiceErrorCode code, string message)
    {
        var (status, errorCode) = code switch
        {
            ServiceErrorCode.NotFound => (StatusCodes.Status404NotFound, "NOT_FOUND"),
            ServiceErrorCode.Conflict => (StatusCodes.Status409Conflict, "CONFLICT"),
            ServiceErrorCode.Unauthorized => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED"),
            ServiceErrorCode.Forbidden => (StatusCodes.Status403Forbidden, "FORBIDDEN"),
            _ => (StatusCodes.Status422UnprocessableEntity, code.ToString().ToUpperInvariant()),
        };

        return new ObjectResult(ApiResponse.Error(errorCode, message)) { StatusCode = status };
    }
}
