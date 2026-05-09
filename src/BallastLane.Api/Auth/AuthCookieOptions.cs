namespace BallastLane.Api.Auth;

public static class AuthCookieOptions
{
    public const string AuthCookieName = "ballastlane.auth";
    public const string CsrfHeaderName = "X-XSRF-TOKEN";
    public const string CsrfCookieName = "XSRF-TOKEN";
    public const string AntiforgeryCookieName = ".ballastlane.antiforgery";

    public static CookieOptions ForAuth(DateTime expiresAtUtc, bool isProduction) => new()
    {
        HttpOnly = true,
        Secure = isProduction,
        SameSite = SameSiteMode.Lax,
        Expires = expiresAtUtc,
        IsEssential = true,
        Path = "/",
    };

    public static CookieOptions ForExpiredAuth(bool isProduction) => new()
    {
        HttpOnly = true,
        Secure = isProduction,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UnixEpoch,
        IsEssential = true,
        Path = "/",
    };

    public static CookieOptions ForCsrf(bool isProduction) => new()
    {
        HttpOnly = false, // Angular reads from JS to set X-XSRF-TOKEN header
        Secure = isProduction,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = "/",
    };
}
