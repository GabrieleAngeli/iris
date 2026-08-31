using Iris.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Application.Tests;

public sealed class ApplicationCompositionTests
{
    [Fact]
    public void AddIrisApplication_builds_a_valid_service_provider()
    {
        var services = new ServiceCollection();

        services.AddIrisApplication();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddIrisApplication_is_idempotent()
    {
        var services = new ServiceCollection();

        services.AddIrisApplication();
        var countAfterFirst = services.Count;
        services.AddIrisApplication();

        Assert.Equal(countAfterFirst, services.Count);
    }
}
