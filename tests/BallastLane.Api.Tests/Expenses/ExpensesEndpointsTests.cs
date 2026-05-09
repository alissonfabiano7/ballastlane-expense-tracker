using System.Net;
using System.Net.Http.Json;
using BallastLane.Api.Auth;
using BallastLane.Api.Tests.TestHost;
using BallastLane.Application.Auth;
using BallastLane.Application.Common;
using BallastLane.Application.Expenses;
using BallastLane.Domain.Expenses;
using BallastLane.Domain.Users;
using Shouldly;

namespace BallastLane.Api.Tests.Expenses;

public class ExpensesEndpointsTests : IClassFixture<BallastLaneApiFactory>
{
    private static readonly DateTime FixedUtcNow = new(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly BallastLaneApiFactory _factory;

    public ExpensesEndpointsTests(BallastLaneApiFactory factory)
    {
        _factory = factory;
        _factory.UserRepository.Reset();
        _factory.ExpenseRepository.Reset();
    }

    [Fact]
    public async Task POST_expenses_creates_resource_and_returns_201()
    {
        (HttpClient client, _) = await LoggedInClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/expenses", new
        {
            amount = 12.34m,
            description = "Coffee",
            category = "Food",
            incurredAt = FixedUtcNow.AddHours(-1),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        ExpenseDto? body = await response.Content.ReadFromJsonAsync<ExpenseDto>();
        body.ShouldNotBeNull();
        body.Amount.ShouldBe(12.34m);
        body.Category.ShouldBe("Food");
        response.Headers.Location?.ToString().ShouldBe($"/expenses/{body.Id}");
    }

    [Fact]
    public async Task POST_expenses_returns_400_for_invalid_input()
    {
        (HttpClient client, _) = await LoggedInClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/expenses", new
        {
            amount = -1m,
            description = (string?)null,
            category = "Yacht",
            incurredAt = FixedUtcNow,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_expenses_returns_400_when_csrf_token_missing()
    {
        SeedUser("demo@ballastlane.test", "Demo@123");
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage login = await client.PostAsJsonAsync(
            "/auth/login", new LoginCommand("demo@ballastlane.test", "Demo@123"));
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Authenticated client (auth cookie + antiforgery cookie tracked
        // automatically) but no X-XSRF-TOKEN header → antiforgery rejects.
        HttpResponseMessage response = await client.PostAsJsonAsync("/expenses", new
        {
            amount = 10m,
            description = "x",
            category = "Food",
            incurredAt = FixedUtcNow,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_expenses_returns_401_without_auth_cookie()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/expenses", new
        {
            amount = 10m,
            description = "x",
            category = "Other",
            incurredAt = FixedUtcNow,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_expenses_returns_only_caller_expenses()
    {
        (HttpClient ownerClient, Guid ownerId) = await LoggedInClientAsync(email: "owner@user.test");
        Guid otherUserId = SeedUser("other@user.test", "Demo@123");
        SeedExpense(ownerId, "owner-1", FixedUtcNow.AddDays(-1));
        SeedExpense(ownerId, "owner-2", FixedUtcNow.AddDays(-2));
        SeedExpense(otherUserId, "other-secret", FixedUtcNow);

        HttpResponseMessage response = await ownerClient.GetAsync("/expenses");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<ExpenseDto>? body = await response.Content.ReadFromJsonAsync<PagedResult<ExpenseDto>>();
        body.ShouldNotBeNull();
        body.TotalCount.ShouldBe(2);
        body.Items.ShouldAllBe(e => e.Description == "owner-1" || e.Description == "owner-2");
    }

    [Fact]
    public async Task GET_expenses_paginates_results()
    {
        (HttpClient client, Guid ownerId) = await LoggedInClientAsync();
        for (int i = 0; i < 5; i++)
        {
            SeedExpense(ownerId, $"e-{i}", FixedUtcNow.AddDays(-i));
        }

        HttpResponseMessage response = await client.GetAsync("/expenses?page=1&pageSize=2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<ExpenseDto>? body = await response.Content.ReadFromJsonAsync<PagedResult<ExpenseDto>>();
        body.ShouldNotBeNull();
        body.TotalCount.ShouldBe(5);
        body.Items.Count.ShouldBe(2);
        body.Page.ShouldBe(1);
        body.PageSize.ShouldBe(2);
    }

    [Fact]
    public async Task GET_expense_by_id_returns_200_for_owner()
    {
        (HttpClient client, Guid ownerId) = await LoggedInClientAsync();
        Guid expenseId = SeedExpense(ownerId, "Lunch", FixedUtcNow);

        HttpResponseMessage response = await client.GetAsync($"/expenses/{expenseId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ExpenseDto? body = await response.Content.ReadFromJsonAsync<ExpenseDto>();
        body.ShouldNotBeNull();
        body.Id.ShouldBe(expenseId);
    }

    [Fact]
    public async Task GET_expense_by_id_returns_404_when_owned_by_other_user()
    {
        (HttpClient client, _) = await LoggedInClientAsync();
        Guid otherUserId = SeedUser("other@user.test", "Demo@123");
        Guid otherExpenseId = SeedExpense(otherUserId, "secret", FixedUtcNow);

        HttpResponseMessage response = await client.GetAsync($"/expenses/{otherExpenseId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PUT_expense_updates_and_returns_200()
    {
        (HttpClient client, Guid ownerId) = await LoggedInClientAsync();
        Guid expenseId = SeedExpense(ownerId, "old", FixedUtcNow);

        HttpResponseMessage response = await client.PutAsJsonAsync($"/expenses/{expenseId}", new
        {
            amount = 99m,
            description = "new",
            category = "Transport",
            incurredAt = FixedUtcNow.AddHours(-2),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ExpenseDto? body = await response.Content.ReadFromJsonAsync<ExpenseDto>();
        body.ShouldNotBeNull();
        body.Amount.ShouldBe(99m);
        body.Description.ShouldBe("new");
        body.Category.ShouldBe("Transport");
    }

    [Fact]
    public async Task PUT_expense_returns_404_when_not_owner()
    {
        (HttpClient client, _) = await LoggedInClientAsync();
        Guid otherUserId = SeedUser("other@user.test", "Demo@123");
        Guid otherExpenseId = SeedExpense(otherUserId, "secret", FixedUtcNow);

        HttpResponseMessage response = await client.PutAsJsonAsync($"/expenses/{otherExpenseId}", new
        {
            amount = 1m,
            description = "hijack",
            category = "Other",
            incurredAt = FixedUtcNow,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_expense_returns_204_for_owner()
    {
        (HttpClient client, Guid ownerId) = await LoggedInClientAsync();
        Guid expenseId = SeedExpense(ownerId, "x", FixedUtcNow);

        HttpResponseMessage response = await client.DeleteAsync($"/expenses/{expenseId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DELETE_expense_returns_404_when_not_owner()
    {
        (HttpClient client, _) = await LoggedInClientAsync();
        Guid otherUserId = SeedUser("other@user.test", "Demo@123");
        Guid otherExpenseId = SeedExpense(otherUserId, "secret", FixedUtcNow);

        HttpResponseMessage response = await client.DeleteAsync($"/expenses/{otherExpenseId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<(HttpClient client, Guid userId)> LoggedInClientAsync(
        string email = "demo@ballastlane.test")
    {
        Guid userId = SeedUser(email, "Demo@123");
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage login = await client.PostAsJsonAsync(
            "/auth/login", new LoginCommand(email, "Demo@123"));
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        // After login, re-fetch antiforgery tokens via /auth/csrf-token now
        // that the request principal is the authenticated user. The
        // login-time tokens were minted while the request principal was
        // still anonymous (the new auth cookie was set on the response, not
        // applied to the current request's HttpContext.User), so they would
        // be rejected on subsequent authenticated mutations because the
        // antiforgery service binds tokens to the user identity.
        HttpResponseMessage csrf = await client.GetAsync("/auth/csrf-token");
        csrf.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Cookies (auth + antiforgery + xsrf) are auto-tracked by
        // HttpClientHandler. Surface the re-issued request token as the
        // X-XSRF-TOKEN header so the antiforgery filter accepts mutations.
        string xsrfToken = csrf.Headers
            .GetValues("Set-Cookie")
            .First(c => c.StartsWith($"{AuthCookieOptions.CsrfCookieName}=", StringComparison.Ordinal))
            .Split(';')[0][$"{AuthCookieOptions.CsrfCookieName}=".Length..];
        client.DefaultRequestHeaders.Add(AuthCookieOptions.CsrfHeaderName, xsrfToken);

        return (client, userId);
    }

    private Guid SeedUser(string email, string password)
    {
        string hash = _factory.PasswordHasher.Hash(password);
        User user = User.Create(email, hash, DateTime.UtcNow);
        _factory.UserRepository.AddAsync(user, default).GetAwaiter().GetResult();
        return user.Id;
    }

    private Guid SeedExpense(Guid userId, string description, DateTime incurredAt)
    {
        Expense expense = Expense.Create(
            userId: userId,
            amount: 10m,
            description: description,
            category: ExpenseCategory.Food,
            incurredAt: incurredAt,
            utcNow: DateTime.UtcNow);
        _factory.ExpenseRepository.AddAsync(expense, default).GetAwaiter().GetResult();
        return expense.Id;
    }
}
