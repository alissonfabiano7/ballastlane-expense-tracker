using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;

namespace BallastLane.Api.Auth;

/// <summary>
/// Endpoint convention helpers for opting JSON-bodied minimal API
/// endpoints into anti-forgery validation.
/// </summary>
/// <remarks>
/// The framework's <c>app.UseAntiforgery()</c> middleware ships with
/// implicit validation for form-bound endpoints only; JSON endpoints
/// have to opt in. The natural opt-in path is endpoint metadata
/// implementing <see cref="IAntiforgeryMetadata"/>, but a custom
/// implementation is not consistently recognized in .NET 10
/// (verified by curl smoke against the running API). Using an explicit
/// endpoint filter that calls <see cref="IAntiforgery.ValidateRequestAsync"/>
/// is the reliable path: it runs after authorization, fires only on
/// mutating methods (POST/PUT/PATCH/DELETE), and lets
/// <see cref="AntiforgeryValidationException"/> bubble up to the
/// global exception handler which maps it to 400.
/// </remarks>
public static class AntiforgeryEndpointExtensions
{
    public static TBuilder RequireAntiforgery<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            string method = ctx.HttpContext.Request.Method;
            if (HttpMethods.IsPost(method) ||
                HttpMethods.IsPut(method) ||
                HttpMethods.IsPatch(method) ||
                HttpMethods.IsDelete(method))
            {
                IAntiforgery antiforgery = ctx.HttpContext.RequestServices
                    .GetRequiredService<IAntiforgery>();
                await antiforgery.ValidateRequestAsync(ctx.HttpContext);
            }
            return await next(ctx);
        });
        return builder;
    }
}
