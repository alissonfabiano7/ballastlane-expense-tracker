namespace BallastLane.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        IHeaderDictionary headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Content-Security-Policy"] = "default-src 'self'";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        return next(context);
    }
}
