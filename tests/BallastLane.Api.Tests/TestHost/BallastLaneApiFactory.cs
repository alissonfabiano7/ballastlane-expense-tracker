using BallastLane.Application.Auth;
using BallastLane.Application.Users;
using BallastLane.Domain.Users;
using BallastLane.Infrastructure.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BallastLane.Api.Tests.TestHost;

public sealed class BallastLaneApiFactory : WebApplicationFactory<Program>
{
    public InMemoryUserRepository UserRepository { get; } = new();
    public Argon2idLikeFakePasswordHasher PasswordHasher { get; } = new();
    public StubTokenService TokenService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Override only what we must — the JWT settings come from
            // appsettings.Development.json so the StubTokenService and
            // JwtBearer pipeline naturally agree on issuer/audience/secret.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sql:ConnectionString"] =
                    "Server=tcp:127.0.0.1,1433;Database=BallastLane_Test;User Id=sa;Password=stub;TrustServerCertificate=True",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            ReplaceSingleton<IUserRepository>(services, UserRepository);
            ReplaceSingleton<IPasswordHasher>(services, PasswordHasher);
            ReplaceSingleton<ITokenService>(services, TokenService);

            // Health checks register via IConfigureOptions<HealthCheckServiceOptions>;
            // PostConfigure runs after the initial registrations to swap in a stub
            // that does not require a live SQL Server in the test process.
            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
                options.Registrations.Add(new HealthCheckRegistration(
                    name: "sqlserver-stub",
                    instance: new StubHealthCheck(),
                    failureStatus: HealthStatus.Unhealthy,
                    tags: ["ready", "db"]));
            });
        });
    }

    private static void ReplaceSingleton<TService>(IServiceCollection services, object instance)
        where TService : class
    {
        ServiceDescriptor? existing = services.SingleOrDefault(d => d.ServiceType == typeof(TService));
        if (existing is not null)
        {
            services.Remove(existing);
        }
        services.AddSingleton(typeof(TService), instance);
    }
}
