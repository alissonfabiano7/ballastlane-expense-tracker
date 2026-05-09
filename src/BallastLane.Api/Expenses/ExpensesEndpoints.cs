using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BallastLane.Api.Auth;
using BallastLane.Application.Common;
using BallastLane.Application.Expenses;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BallastLane.Api.Expenses;

public static class ExpensesEndpoints
{
    public static IEndpointRouteBuilder MapExpensesEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/expenses")
            .WithTags("Expenses")
            .RequireAuthorization()
            .RequireAntiforgery();

        group.MapPost("/", CreateAsync).WithName("CreateExpense");
        group.MapGet("/", ListAsync).WithName("ListExpenses");
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetExpense");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateExpense");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteExpense");

        return app;
    }

    private static async Task<Created<ExpenseDto>> CreateAsync(
        CreateExpenseCommand command,
        CreateExpenseUseCase useCase,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        Guid userId = ResolveUserId(user);
        ExpenseDto dto = await useCase.HandleAsync(userId, command, cancellationToken);
        return TypedResults.Created($"/expenses/{dto.Id}", dto);
    }

    private static async Task<Ok<PagedResult<ExpenseDto>>> ListAsync(
        ListExpensesUseCase useCase,
        ClaimsPrincipal user,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        Guid userId = ResolveUserId(user);
        PagedResult<ExpenseDto> result = await useCase.HandleAsync(
            userId, new ListExpensesQuery(page, pageSize), cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<ExpenseDto>> GetByIdAsync(
        Guid id,
        GetExpenseByIdUseCase useCase,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        Guid userId = ResolveUserId(user);
        ExpenseDto dto = await useCase.HandleAsync(userId, id, cancellationToken);
        return TypedResults.Ok(dto);
    }

    private static async Task<Ok<ExpenseDto>> UpdateAsync(
        Guid id,
        UpdateExpenseCommand command,
        UpdateExpenseUseCase useCase,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        Guid userId = ResolveUserId(user);
        ExpenseDto dto = await useCase.HandleAsync(userId, id, command, cancellationToken);
        return TypedResults.Ok(dto);
    }

    private static async Task<NoContent> DeleteAsync(
        Guid id,
        DeleteExpenseUseCase useCase,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        Guid userId = ResolveUserId(user);
        await useCase.HandleAsync(userId, id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static Guid ResolveUserId(ClaimsPrincipal user)
    {
        string? sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (sub is null || !Guid.TryParse(sub, out Guid userId))
        {
            throw new InvalidOperationException(
                "Authenticated principal is missing a valid 'sub' claim.");
        }
        return userId;
    }
}
