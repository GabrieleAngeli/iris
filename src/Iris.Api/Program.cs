using System.Reflection;
using Iris.Api;
using Iris.Api.Auth;
using Iris.Api.Authorization;
using Iris.Api.Endpoints;
using Iris.Application;
using Iris.Application.Abstractions;
using Iris.Contracts.Meta;
using Iris.Infrastructure;
using Iris.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog is the provider; ILogger<T> (already native to every project) stays the
// abstraction. Sinks — where logs go — are entirely configuration-driven (the "Serilog"
// section in appsettings), so adding a remote sink later needs no code change here.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();

builder.Services.AddIrisApplication();
builder.Services.AddIrisInfrastructure(builder.Configuration);

builder.AddIrisAuthentication();
builder.Services.AddIrisAuthorization();
builder.Services.AddScoped<ICurrentUser, ClaimsPrincipalCurrentUser>();
builder.Services.AddScoped<IClaimsTransformation, AccessProvisioningClaimsTransformation>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", (IHostEnvironment env) => new ServiceInfoResponse(
        Name: "Iris — Infrastructure Control Plane",
        Version: Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0",
        Environment: env.EnvironmentName))
    .WithName("GetServiceInfo")
    .WithSummary("Identity of the running Iris API instance.")
    .AllowAnonymous();

app.MapHealthChecks("/health").WithName("GetHealth").AllowAnonymous();

app.MapAccessEndpoints();
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapSystemSettingsEndpoints();
app.MapActivityEndpoints();
app.MapGovernanceEndpoints();
app.MapInfrastructureEndpoints();
app.MapApplicationsEndpoints();
app.MapSetupEndpoints();

if (builder.Configuration.GetValue("Iris:Database:MigrateOnStartup", true))
{
    // Off by default: a real deployment starts genuinely empty and the first-run setup wizard
    // (GET /setup/status, POST /setup/complete) is what creates its first super-admin. On for
    // local development (appsettings.Development.json) so the existing reference tenancy —
    // Contoso/Globex, admin@iris.local already a platform-admin — keeps showing up as before.
    var seedDemoData = builder.Configuration.GetValue("Iris:Database:SeedDemoData", false);
    await IrisDbInitializer.MigrateAndSeedAsync(app.Services, seedDemoData);
}

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> integration tests can boot the API.</summary>
public partial class Program;
