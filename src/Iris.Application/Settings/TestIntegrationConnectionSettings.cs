using Iris.Application.Abstractions;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

/// <summary>
/// Five thin handlers backing <c>POST /system/integrations/{key}/test</c> — one per integration,
/// each just delegating to <see cref="IIntegrationConnectionTester"/> and mapping its
/// <see cref="IntegrationConnectorStatus"/> down to a <see cref="TestIntegrationConnectionResponse"/>.
/// Reuse the exact same command records as the corresponding <c>Save*IntegrationSettingsHandler</c>
/// — a "test" and a "save" take identical input, they just do different things with it.
/// </summary>
public sealed class TestOpenBaoIntegrationSettingsHandler(IIntegrationConnectionTester tester)
{
    public async Task<TestIntegrationConnectionResponse> HandleAsync(
        SaveOpenBaoIntegrationSettingsCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var status = await tester.TestOpenBaoAsync(command, cancellationToken).ConfigureAwait(false);
        // OpenBao alone: "Configured" (endpoint set, no token yet) is a legitimate pass too — the
        // existing Save flow explicitly supports saving the endpoint first and adding the token
        // later via Promote/Provision (see SaveOpenBaoIntegrationSettingsHandler's own message).
        var succeeded = status.Status is "Reachable" or "Configured";
        return new TestIntegrationConnectionResponse(succeeded, status.Status, status.Message);
    }
}

public sealed class TestAwxIntegrationSettingsHandler(IIntegrationConnectionTester tester)
{
    public async Task<TestIntegrationConnectionResponse> HandleAsync(
        SaveAwxIntegrationSettingsCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var status = await tester.TestAwxAsync(command, cancellationToken).ConfigureAwait(false);
        return new TestIntegrationConnectionResponse(status.Status == "Reachable", status.Status, status.Message);
    }
}

public sealed class TestAzureDevOpsIntegrationSettingsHandler(IIntegrationConnectionTester tester)
{
    public async Task<TestIntegrationConnectionResponse> HandleAsync(
        SaveAzureDevOpsIntegrationSettingsCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var status = await tester.TestAzureDevOpsAsync(command, cancellationToken).ConfigureAwait(false);
        return new TestIntegrationConnectionResponse(status.Status == "Reachable", status.Status, status.Message);
    }
}

public sealed class TestNexusIntegrationSettingsHandler(IIntegrationConnectionTester tester)
{
    public async Task<TestIntegrationConnectionResponse> HandleAsync(
        SaveNexusIntegrationSettingsCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var status = await tester.TestNexusAsync(command, cancellationToken).ConfigureAwait(false);
        return new TestIntegrationConnectionResponse(status.Status == "Reachable", status.Status, status.Message);
    }
}

public sealed class TestOpsHostIntegrationSettingsHandler(IIntegrationConnectionTester tester)
{
    public async Task<TestIntegrationConnectionResponse> HandleAsync(
        SaveOpsHostIntegrationSettingsCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var status = await tester.TestOpsHostAsync(command, cancellationToken).ConfigureAwait(false);
        return new TestIntegrationConnectionResponse(status.Status == "Reachable", status.Status, status.Message);
    }
}
