using System.Text.Json;
using BallastLane.Application.Auth;
using BallastLane.Application.Common;
using BallastLane.Domain.Common;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace BallastLane.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        (int status, string title, IReadOnlyDictionary<string, string[]>? errors) = ex switch
        {
            ValidationException v => (
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                v.Errors),
            DomainValidationException d => (
                StatusCodes.Status400BadRequest,
                d.Message,
                (IReadOnlyDictionary<string, string[]>?)null),
            InvalidCredentialsException => (
                StatusCodes.Status401Unauthorized,
                ex.Message,
                (IReadOnlyDictionary<string, string[]>?)null),
            NotFoundException n => (
                StatusCodes.Status404NotFound,
                n.Message,
                (IReadOnlyDictionary<string, string[]>?)null),
            ConflictException c => (
                StatusCodes.Status409Conflict,
                c.Message,
                (IReadOnlyDictionary<string, string[]>?)null),
            AntiforgeryValidationException => (
                StatusCodes.Status400BadRequest,
                "Invalid or missing anti-forgery token.",
                (IReadOnlyDictionary<string, string[]>?)null),
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                (IReadOnlyDictionary<string, string[]>?)null),
        };

        if (status >= 500)
        {
            logger.LogError(ex, "Unhandled exception while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogWarning(ex, "Handled application exception while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }

        ProblemDetails problem = new()
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.io/{status}",
            Instance = context.Request.Path,
        };
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }
        if (environment.IsDevelopment() && status >= 500)
        {
            problem.Detail = ex.ToString();
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
