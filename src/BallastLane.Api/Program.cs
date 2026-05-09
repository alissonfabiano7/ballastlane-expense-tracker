using System.Text;
using BallastLane.Api;
using BallastLane.Api.Auth;
using BallastLane.Api.Expenses;
using BallastLane.Api.Middleware;
using BallastLane.Application.Auth;
using BallastLane.Application.Expenses;
using BallastLane.Infrastructure;
using BallastLane.Infrastructure.Configuration;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// --- Logging (Serilog) ---
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// --- Kestrel hardening ---
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

// --- Layers ---
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTransient<RegisterUserUseCase>();
builder.Services.AddTransient<LoginUseCase>();
builder.Services.AddTransient<CreateExpenseUseCase>();
builder.Services.AddTransient<UpdateExpenseUseCase>();
builder.Services.AddTransient<GetExpenseByIdUseCase>();
builder.Services.AddTransient<ListExpensesUseCase>();
builder.Services.AddTransient<DeleteExpenseUseCase>();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserCommandValidator>();

// --- Auth (JWT in HttpOnly cookie) ---
JwtSettings jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt section is missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Cookies.TryGetValue(AuthCookieOptions.AuthCookieName, out string? token)
                    && !string.IsNullOrEmpty(token))
                {
                    ctx.Token = token;
                }
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                // Suppress the WWW-Authenticate header — we use cookies, not Bearer prompts.
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// --- Anti-CSRF ---
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = AuthCookieOptions.CsrfHeaderName;
    options.Cookie.Name = ".ballastlane.antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
});

// --- CORS ---
string[] allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("BallastLaneSpa", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
        .WithExposedHeaders(AuthCookieOptions.CsrfCookieName)
        .AllowCredentials());
});

// --- OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// --- Pipeline ---
WebApplication app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("BallastLaneSpa");
app.UseAuthentication();
app.UseAuthorization();

// --- Health endpoints ---
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false, // liveness: only the host's existence
    AllowCachingResponses = false,
    ResponseWriter = HealthCheckWriters.WriteJson,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    AllowCachingResponses = false,
    ResponseWriter = HealthCheckWriters.WriteJson,
});

// --- Endpoints ---
app.MapAuthEndpoints();
app.MapExpensesEndpoints();
app.MapOpenApi();

await app.RunAsync();

// Test host hook (referenced from BallastLane.Api.Tests via WebApplicationFactory<Program>)
public partial class Program { }
