using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ToysAuctionHouse.Api.Middleware;

public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value is { Errors.Count: > 0 })
                .SelectMany(x => x.Value!.Errors.Select(e => x.Key + ": " + e.ErrorMessage))
                .ToList();

            context.Result = new ObjectResult(ApiResponse.Error("VALIDATION_FAILED", string.Join("; ", errors)))
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity,
            };
            return;
        }

        await next();
    }
}
