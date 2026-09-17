using Iris.Application.Abstractions;
using Iris.Application.Settings;
using Iris.Domain.Settings;
using Iris.Infrastructure.Integrations;

namespace Iris.Infrastructure.Tests.Integrations;

public sealed class IntegrationConnectionTesterTests
{
    // A loopback port nothing listens on: connections fail immediately (ECONNREFUSED) with no DNS
    // lookup, so tests that need a real connector to reach its HTTP-attempt branch stay fast and
    // deterministic without depending on any real external service.
    private const string UnreachableEndpoint = "http://127.0.0.1:1";

    private sealed class RecordingSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _byReference = [];

        public void Seed(string reference, string value) => _byReference[reference] = value;

        public Task<string> StoreAsync(string logicalPath, string secretValue, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The tester never stores anything — it only probes candidate values.");

        public Task<string?> RetrieveAsync(string reference, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byReference.GetValueOrDefault(reference));

        public Task DeleteAsync(string reference, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSettingsRepository(IntegrationSettings settings) : IIntegrationSettingsRepository
    {
        public Task<IntegrationSettings?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<IntegrationSettings?>(settings);

        public Task<IntegrationSettings> GetOrCreateAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);
    }

    private sealed class FakeSecretStorePromotion(bool isOpenBaoActive = false) : ISecretStorePromotion
    {
        public bool IsOpenBaoActive { get; } = isOpenBaoActive;

        public Task<int> PromoteToOpenBaoAsync(string endpoint, string token, string mountPath, bool useKvV2, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static IntegrationConnectionTester CreateTester(
        out RecordingSecretStore secretStore, IntegrationSettings? settings = null, bool isOpenBaoActive = false)
    {
        secretStore = new RecordingSecretStore();
        return new IntegrationConnectionTester(
            new FakeSettingsRepository(settings ?? IntegrationSettings.CreateEmpty()),
            secretStore,
            new FakeSecretStorePromotion(isOpenBaoActive));
    }

    [Fact]
    public async Task TestOpenBao_with_no_endpoint_reports_not_configured_without_touching_the_network()
    {
        var tester = CreateTester(out _);

        var status = await tester.TestOpenBaoAsync(new SaveOpenBaoIntegrationSettingsCommand(string.Empty, null, "secret", true));

        Assert.Equal("Not configured", status.Status);
    }

    [Fact]
    public async Task TestOpenBao_blank_token_with_nothing_stored_is_the_soft_configured_pass()
    {
        // OpenBao's own token can never be resolved from a persisted reference (see
        // IntegrationOptionsFactory's comment on the same constraint) — leaving Token blank with
        // nothing stored yet, and OpenBao not promoted, is exactly the supported "save the
        // endpoint now, add the token later via Promote" flow.
        var tester = CreateTester(out _);

        var status = await tester.TestOpenBaoAsync(new SaveOpenBaoIntegrationSettingsCommand("https://openbao.example", null, "secret", true));

        Assert.Equal("Configured", status.Status);
    }

    [Fact]
    public async Task TestOpenBao_blank_token_falls_back_to_the_stored_reference_and_then_really_probes()
    {
        var settings = IntegrationSettings.CreateEmpty();
        settings.ConfigureOpenBao("https://openbao.example", "ref://openbao/token", "secret", true);
        var tester = CreateTester(out var secretStore, settings);
        secretStore.Seed("ref://openbao/token", "resolved-token");

        var status = await tester.TestOpenBaoAsync(
            new SaveOpenBaoIntegrationSettingsCommand(UnreachableEndpoint, null, "secret", true));

        // Reaching "Unreachable" (rather than the token-missing "Configured" gray zone) proves the
        // resolved token made it into the candidate options and a real probe was attempted.
        Assert.Equal("Unreachable", status.Status);
    }

    [Fact]
    public async Task TestAwx_with_no_endpoint_reports_not_configured()
    {
        var tester = CreateTester(out _);

        var status = await tester.TestAwxAsync(new SaveAwxIntegrationSettingsCommand(string.Empty, null, null));

        Assert.Equal("Not configured", status.Status);
    }

    [Fact]
    public async Task TestAwx_blank_token_falls_back_to_the_stored_reference_and_reports_why_it_cannot_resolve_it()
    {
        var settings = IntegrationSettings.CreateEmpty();
        settings.ConfigureAwx("https://awx.example", "ref://awx/token", 7);
        var tester = CreateTester(out _, settings); // nothing seeded — the reference can't resolve

        var status = await tester.TestAwxAsync(new SaveAwxIntegrationSettingsCommand("https://awx.example", null, 7));

        // AwxClient resolves the token lazily via ISecretStore before ever attempting HTTP — an
        // unresolvable reference reports "Unreachable" without any network call, proving the
        // reference (not a literal blank) reached the candidate options.
        Assert.Equal("Unreachable", status.Status);
    }

    [Fact]
    public async Task TestAzureDevOps_with_no_endpoint_reports_not_configured()
    {
        var tester = CreateTester(out _);

        var status = await tester.TestAzureDevOpsAsync(new SaveAzureDevOpsIntegrationSettingsCommand(string.Empty, null));

        Assert.Equal("Not configured", status.Status);
    }

    [Fact]
    public async Task TestAzureDevOps_short_circuits_on_org_unreachable_without_attempting_the_manifest_read()
    {
        var tester = CreateTester(out _);

        var status = await tester.TestAzureDevOpsAsync(new SaveAzureDevOpsIntegrationSettingsCommand(
            UnreachableEndpoint, "pat", "Refactoring_ops_flow", "awx"));

        Assert.Equal("Unreachable", status.Status);
    }

    [Fact]
    public async Task TestAzureDevOps_blank_token_falls_back_to_the_stored_reference()
    {
        var settings = IntegrationSettings.CreateEmpty();
        settings.ConfigureAzureDevOps(UnreachableEndpoint, "ref://azure-devops/token");
        var tester = CreateTester(out var secretStore, settings);
        secretStore.Seed("ref://azure-devops/token", "resolved-pat");

        var status = await tester.TestAzureDevOpsAsync(new SaveAzureDevOpsIntegrationSettingsCommand(UnreachableEndpoint, null));

        // No token at all would short-circuit to "Configured" without ever touching the network
        // (AzureDevOpsConnector requires a PAT for any real call) — reaching "Unreachable" here
        // proves the resolved token was used to attempt a real probe.
        Assert.Equal("Unreachable", status.Status);
    }

    [Fact]
    public async Task TestNexus_with_no_endpoint_reports_not_configured()
    {
        var tester = CreateTester(out _);

        var status = await tester.TestNexusAsync(new SaveNexusIntegrationSettingsCommand(string.Empty, null));

        Assert.Equal("Not configured", status.Status);
    }

    [Fact]
    public async Task TestNexus_probes_the_given_endpoint()
    {
        var tester = CreateTester(out _);

        var status = await tester.TestNexusAsync(new SaveNexusIntegrationSettingsCommand(UnreachableEndpoint, null));

        Assert.Equal("Unreachable", status.Status);
    }

    [Fact]
    public async Task TestOpsHost_with_no_credentials_reports_not_configured()
    {
        var tester = CreateTester(out _);

        var status = await tester.TestOpsHostAsync(new SaveOpsHostIntegrationSettingsCommand(string.Empty, 22, string.Empty, "SshKey", null));

        Assert.Equal("Not configured", status.Status);
    }

    [Fact]
    public async Task TestOpsHost_blank_secret_falls_back_to_the_stored_reference_and_reports_why_it_cannot_resolve_it()
    {
        var settings = IntegrationSettings.CreateEmpty();
        settings.ConfigureOpsHost("opshost.example", 22, "ops", Iris.Domain.Infrastructure.ServerCredentialAuthMethod.SshKey, "ref://ops-host/secret");
        var tester = CreateTester(out _, settings); // nothing seeded — the reference can't resolve

        var status = await tester.TestOpsHostAsync(new SaveOpsHostIntegrationSettingsCommand("opshost.example", 22, "ops", "SshKey", null));

        Assert.Equal("Unreachable", status.Status);
    }

    [Fact]
    public async Task TestOpsHost_with_an_unknown_auth_method_reports_not_configured()
    {
        var tester = CreateTester(out _);

        var status = await tester.TestOpsHostAsync(new SaveOpsHostIntegrationSettingsCommand("opshost.example", 22, "ops", "Bogus", "secret"));

        Assert.Equal("Not configured", status.Status);
    }
}
