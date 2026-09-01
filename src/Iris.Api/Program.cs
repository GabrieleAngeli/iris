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

var builder = WebApplication.CreateBuilder(args);

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
app.MapGovernanceEndpoints();
app.MapInfrastructureEndpoints();
app.MapApplicationsEndpoints();

if (builder.Configuration.GetValue("Iris:Database:MigrateOnStartup", true))
{
    await IrisDbInitializer.MigrateAndSeedAsync(app.Services);
}

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> integration tests can boot the API.</summary>
public partial class Program;
