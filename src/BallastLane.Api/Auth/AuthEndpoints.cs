using System.Security.Claims;
using BallastLane.Application.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BallastLane.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .WithName("RegisterUser");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("LoginUser");

        group.MapPost("/logout", Logout)
            .RequireAuthorization()
            .WithName("LogoutUser");

        group.MapGet("/csrf-token", IssueCsrfToken)
            .WithName("IssueCsrfToken");

        group.MapGet("/me", Me)
            .RequireAuthorization()
            .WithName("CurrentUser");

        return app;
    }

    private static async Task<Results<Ok<UserResponse>, ProblemHttpResult>> RegisterAsync(
        RegisterUserCommand command,
        RegisterUserUseCase useCase,
        HttpContext httpContext,
        IHostEnvironment environment,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        AuthResult result = await useCase.HandleAsync(command, cancellationToken);
        IssueAuthCookies(httpContext, result, environment, antiforgery);
        return TypedResults.Ok(new UserResponse(result.UserId, result.Email, result.ExpiresAtUtc));
    }

    private static async Task<Results<Ok<UserResponse>, ProblemHttpResult>> LoginAsync(
        LoginCommand command,
        LoginUseCase useCase,
        HttpContext httpContext,
        IHostEnvironment environment,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        AuthResult result = await useCase.HandleAsync(command, cancellationToken);
        IssueAuthCookies(httpContext, result, environment, antiforgery);
        return TypedResults.Ok(new UserResponse(result.UserId, result.Email, result.ExpiresAtUtc));
    }

    private static IResult Logout(HttpContext httpContext, IHostEnvironment environment)
    {
        httpContext.Response.Cookies.Delete(
            AuthCookieOptions.AuthCookieName,
            AuthCookieOptions.ForExpiredAuth(environment.IsProduction()));
        httpContext.Response.Cookies.Delete(AuthCookieOptions.CsrfCookieName);
        return TypedResults.NoContent();
    }

    private static IResult IssueCsrfToken(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        IHostEnvironment environment)
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(httpContext);
        if (!string.IsNullOrEmpty(tokens.RequestToken))
        {
            httpContext.Response.Cookies.Append(
                AuthCookieOptions.CsrfCookieName,
                tokens.RequestToken,
                AuthCookieOptions.ForCsrf(environment.IsProduction()));
        }
        return TypedResults.Ok(new { csrfTokenIssued = true });
    }

    private static IResult Me(ClaimsPrincipal user)
    {
        string? id = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        string? email = user.FindFirstValue("email") ?? user.FindFirstValue(ClaimTypes.Email);
        if (id is null || email is null)
        {
            return TypedResults.Unauthorized();
        }
        return TypedResults.Ok(new { userId = Guid.Parse(id), email });
    }

    private static void IssueAuthCookies(
        HttpContext httpContext,
        AuthResult result,
        IHostEnvironment environment,
        IAntiforgery antiforgery)
    {
        bool isProd = environment.IsProduction();

        httpContext.Response.Cookies.Append(
            AuthCookieOptions.AuthCookieName,
            result.Token,
            AuthCookieOptions.ForAuth(result.ExpiresAtUtc, isProd));

        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(httpContext);
        if (!string.IsNullOrEmpty(tokens.RequestToken))
        {
            httpContext.Response.Cookies.Append(
                AuthCookieOptions.CsrfCookieName,
                tokens.RequestToken,
                AuthCookieOptions.ForCsrf(isProd));
        }
    }
}

public sealed record UserResponse(Guid UserId, string Email, DateTime ExpiresAtUtc);
