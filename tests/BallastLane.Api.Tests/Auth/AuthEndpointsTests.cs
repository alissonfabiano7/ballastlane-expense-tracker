using System.Net;
using System.Net.Http.Json;
using BallastLane.Api.Auth;
using BallastLane.Api.Tests.TestHost;
using BallastLane.Application.Auth;
using BallastLane.Domain.Users;
using Shouldly;

namespace BallastLane.Api.Tests.Auth;

public class AuthEndpointsTests : IClassFixture<BallastLaneApiFactory>
{
    private readonly BallastLaneApiFactory _factory;

    public AuthEndpointsTests(BallastLaneApiFactory factory)
    {
        _factory = factory;
        _factory.UserRepository.Reset();
    }

    [Fact]
    public async Task POST_register_creates_user_and_returns_auth_cookie()
    {
        using HttpClient client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterUserCommand("new@user.test", "Demo@123"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies).ShouldBeTrue();
        cookies!.ShouldContain(c =>
            c.StartsWith($"{AuthCookieOptions.AuthCookieName}=", StringComparison.Ordinal)
            && c.Contains("httponly", StringComparison.OrdinalIgnoreCase)
            && c.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));

        UserResponse? body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.ShouldNotBeNull();
        body.Email.ShouldBe("new@user.test");
    }

    [Fact]
    public async Task POST_register_returns_409_when_email_already_registered()
    {
        SeedUser("dup@user.test", "Demo@123");

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterUserCommand("dup@user.test", "Demo@123"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_register_returns_400_for_invalid_input()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterUserCommand("not-an-email", "weak"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_login_returns_auth_cookie_on_valid_credentials()
    {
        SeedUser("demo@ballastlane.test", "Demo@123");

        using HttpClient client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginCommand("demo@ballastlane.test", "Demo@123"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("Set-Cookie")
            .ShouldContain(c => c.StartsWith($"{AuthCookieOptions.AuthCookieName}=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task POST_login_returns_401_for_wrong_password()
    {
        SeedUser("demo@ballastlane.test", "Demo@123");

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginCommand("demo@ballastlane.test", "Wrong@123"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_login_returns_401_for_unknown_email()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginCommand("missing@user.test", "Demo@123"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_me_returns_401_without_cookie()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_me_returns_user_info_with_valid_cookie()
    {
        SeedUser("demo@ballastlane.test", "Demo@123");

        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage login = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginCommand("demo@ballastlane.test", "Demo@123"));
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        string? authCookie = login.Headers
            .GetValues("Set-Cookie")
            .FirstOrDefault(c => c.StartsWith($"{AuthCookieOptions.AuthCookieName}=", StringComparison.Ordinal));
        authCookie.ShouldNotBeNull();
        string cookieValue = authCookie!.Split(';')[0];

        using HttpRequestMessage request = new(HttpMethod.Get, "/auth/me");
        request.Headers.Add("Cookie", cookieValue);
        HttpResponseMessage me = await client.SendAsync(request);

        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private void SeedUser(string email, string password)
    {
        string hash = _factory.PasswordHasher.Hash(password);
        User user = User.Create(email, hash, DateTime.UtcNow);
        _factory.UserRepository.AddAsync(user, default).GetAwaiter().GetResult();
    }
}
