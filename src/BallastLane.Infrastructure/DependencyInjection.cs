using BallastLane.Application.Auth;
using BallastLane.Application.Expenses;
using BallastLane.Application.Users;
using BallastLane.Infrastructure.Configuration;
using BallastLane.Infrastructure.HealthChecks;
using BallastLane.Infrastructure.Persistence;
using BallastLane.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BallastLane.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<SqlSettings>()
            .BindConfiguration(SqlSettings.SectionName)
            .Validate(s => !string.IsNullOrWhiteSpace(s.ConnectionString),
                "Sql:ConnectionString is required.")
            .ValidateOnStart();

        services
            .AddOptions<JwtSettings>()
            .BindConfiguration(JwtSettings.SectionName)
            .Validate(s => !string.IsNullOrWhiteSpace(s.Secret) && s.Secret.Length >= 32,
                "Jwt:Secret must be at least 32 characters.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.Issuer), "Jwt:Issuer is required.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.Audience), "Jwt:Audience is required.")
            .Validate(s => s.ExpirationMinutes > 0, "Jwt:ExpirationMinutes must be positive.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<SqlExecutor>(sp =>
        {
            SqlSettings settings = sp.GetRequiredService<IOptions<SqlSettings>>().Value;
            ILogger<SqlExecutor> logger = sp.GetRequiredService<ILogger<SqlExecutor>>();
            return new SqlExecutor(settings.ConnectionString, logger);
        });

        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>(
                name: "sqlserver",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "db"]);

        return services;
    }
}
