using Iris.Application.Applications;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Tests.Applications;

public sealed class AnsibleScaffoldGeneratorTests
{
    private static readonly DateTimeOffset GeneratedAt = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private static (ApplicationDefinition Application, ApplicationVersion Version) BuildVersion(
        IReadOnlyList<NewConfigurationKey> keys,
        IReadOnlyList<NewApplicationUnitDefinition>? units = null)
    {
        var application = new ApplicationDefinition(
            Guid.NewGuid(),
            "AugeG4 Engine",
            "augeg4-engine",
            ApplicationRuntimeType.Java,
            "https://git.example/augeg4-engine",
            "main",
            null);
        var runtimeMetadata = new RuntimeMetadata("java17", null, null, null, null);
        var version = application.AddVersion(Guid.NewGuid(), "4.0.0", "refs/tags/4.0.0", runtimeMetadata);
        version.ApplyImport(
            "1.1",
            "{}",
            keys,
            [],
            [],
            units ?? [],
            [],
            [],
            [],
            GeneratedAt);
        return (application, version);
    }

    private static NewConfigurationKey Key(
        string key,
        string targetKind,
        bool required = true,
        bool secret = false,
        string? defaultValue = null,
        string? valueType = null,
        string? placeholderKey = null,
        string? description = null) =>
        new(Guid.NewGuid(), key, targetKind, required, secret, defaultValue, description, null, placeholderKey, valueType);

    private static AnsibleScaffoldFileResponse RequireFile(AnsibleScaffoldResponse response, string relativePath) =>
        response.Files.SingleOrDefault(file => file.RelativePath == relativePath)
            ?? throw new Xunit.Sdk.XunitException($"No generated file at '{relativePath}'. Files: {string.Join(", ", response.Files.Select(f => f.RelativePath))}");

    [Fact]
    public void Flat_target_renders_key_equals_jinja_placeholder_lines()
    {
        var (application, version) = BuildVersion([
            Key("spring.datasource.url", "application.properties", secret: true, valueType: "connectionString")
        ]);

        var response = AnsibleScaffoldGenerator.Generate(application, version, GeneratedAt);

        var file = RequireFile(response, "roles/augeg4-engine/templates/application.properties.j2");
        Assert.Contains("spring.datasource.url={{ iris_spring_datasource_url }}", file.Content);
    }

    [Fact]
    public void Json_target_nests_dotted_keys_and_respects_value_type_quoting()
    {
        var (application, version) = BuildVersion([
            Key("ConnectionStrings:Main", "appsettings.json", secret: true, valueType: "connectionString"),
            Key("Server:Port", "appsettings.json", valueType: "integer")
        ]);

        var response = AnsibleScaffoldGenerator.Generate(application, version, GeneratedAt);

        var file = RequireFile(response, "roles/augeg4-engine/templates/appsettings.json.j2");
        Assert.Contains("\"ConnectionStrings\": {", file.Content);
        Assert.Contains("\"Main\": \"{{ iris_connectionstrings_main }}\"", file.Content);
        Assert.Contains("\"Server\": {", file.Content);
        Assert.Contains("\"Port\": {{ iris_server_port }}", file.Content);
    }

    [Fact]
    public void Structurally_ambiguous_target_never_guesses_at_real_syntax()
    {
        var (application, version) = BuildVersion([
            Key("MailSettings/Host", "Web.config", description: "SMTP host")
        ]);

        var response = AnsibleScaffoldGenerator.Generate(application, version, GeneratedAt);

        var file = RequireFile(response, "roles/augeg4-engine/templates/Web.config.j2");
        Assert.Contains("does not generate this format's real syntax", file.Content);
        Assert.Contains("MailSettings/Host -> iris_mailsettings_host", file.Content);
        Assert.DoesNotContain("<", file.Content);
    }

    [Fact]
    public void Dockerfile_and_compose_targets_render_as_snippets_not_whole_files()
    {
        var (application, version) = BuildVersion([
            Key("ASPNETCORE_ENVIRONMENT", "dockerfile:ENV"),
            Key("REDIS_HOST", "compose:environment")
        ]);

        var response = AnsibleScaffoldGenerator.Generate(application, version, GeneratedAt);

        var dockerfileSnippet = RequireFile(response, "roles/augeg4-engine/templates/snippets/dockerfile-env.j2");
        Assert.Contains("Dockerfile ENV block", dockerfileSnippet.Description);
        Assert.Contains("ASPNETCORE_ENVIRONMENT={{ iris_aspnetcore_environment }}", dockerfileSnippet.Content);

        var composeSnippet = RequireFile(response, "roles/augeg4-engine/templates/snippets/compose-environment.j2");
        Assert.Contains("environment:", composeSnippet.Description);
        Assert.Contains("REDIS_HOST={{ iris_redis_host }}", composeSnippet.Content);

        Assert.DoesNotContain(response.Files, file => file.RelativePath.Contains("dockerfile:ENV", StringComparison.Ordinal));
    }

    [Fact]
    public void Code_only_targets_get_no_template_but_are_documented()
    {
        var (application, version) = BuildVersion([
            Key("Features:UseNewCheckout", "code:IConfiguration", description: "Feature flag read directly in code")
        ]);

        var response = AnsibleScaffoldGenerator.Generate(application, version, GeneratedAt);

        Assert.DoesNotContain(response.Files, file => file.RelativePath.Contains("templates/", StringComparison.Ordinal) &&
            file.Content.Contains("Features:UseNewCheckout", StringComparison.Ordinal));

        var defaults = RequireFile(response, "roles/augeg4-engine/defaults/main.yml");
        Assert.Contains("iris_features_usenewcheckout:", defaults.Content);
        Assert.Contains("not file-backed", defaults.Content);

        var readme = RequireFile(response, "README.md");
        Assert.Contains("Features:UseNewCheckout", readme.Content);
        Assert.Contains("iris_features_usenewcheckout", readme.Content);
    }

    [Fact]
    public void Docker_execution_target_produces_container_task()
    {
        var (application, version) = BuildVersion(
            [],
            [new NewApplicationUnitDefinition(Guid.NewGuid(), "augeg4.engine.master", "Master", null, null, null, """["docker"]""", null)]);

        var response = AnsibleScaffoldGenerator.Generate(application, version, GeneratedAt);

        var tasks = RequireFile(response, "roles/augeg4-engine/tasks/main.yml");
        Assert.Contains("Deploy container augeg4.engine.master", tasks.Content);
        Assert.Contains("community.docker.docker_container:", tasks.Content);
        Assert.DoesNotContain("Manage service augeg4.engine.master", tasks.Content);
    }

    [Fact]
    public void Service_kind_with_no_execution_targets_produces_systemd_task()
    {
        var (application, version) = BuildVersion(
            [],
            [new NewApplicationUnitDefinition(Guid.NewGuid(), "augeg4.engine.master", "Master", "service", null, null, null, null)]);

        var response = AnsibleScaffoldGenerator.Generate(application, version, GeneratedAt);

        var tasks = RequireFile(response, "roles/augeg4-engine/tasks/main.yml");
        Assert.Contains("Manage service augeg4.engine.master", tasks.Content);
        Assert.Contains("ansible.builtin.systemd_service:", tasks.Content);
        Assert.DoesNotContain("Deploy container augeg4.engine.master", tasks.Content);
    }

    [Fact]
    public void Variable_names_prefer_placeholder_key_over_raw_key_exactly_like_the_ansible_plan()
    {
        var (application, version) = BuildVersion([
            Key("mailPort", "application.properties", valueType: "integer", placeholderKey: "domain.augeg4.smtp.system.port")
        ]);

        var response = AnsibleScaffoldGenerator.Generate(application, version, GeneratedAt);

        var file = RequireFile(response, "roles/augeg4-engine/templates/application.properties.j2");
        Assert.Contains("mailPort={{ iris_domain_augeg4_smtp_system_port }}", file.Content);
        Assert.DoesNotContain("iris_mailport", file.Content);
    }

    [Fact]
    public void Response_carries_application_slug_version_and_generation_timestamp()
    {
        var (application, version) = BuildVersion([Key("a", "application.properties")]);

        var response = AnsibleScaffoldGenerator.Generate(application, version, GeneratedAt);

        Assert.Equal("augeg4-engine", response.ApplicationSlug);
        Assert.Equal("4.0.0", response.Version);
        Assert.Equal(GeneratedAt, response.GeneratedAtUtc);
    }

    [Fact]
    public void Playbook_and_role_skeleton_files_are_always_present_even_with_no_keys()
    {
        var (application, version) = BuildVersion([]);

        var response = AnsibleScaffoldGenerator.Generate(application, version, GeneratedAt);

        RequireFile(response, "playbooks/augeg4-engine.yml");
        RequireFile(response, "roles/augeg4-engine/defaults/main.yml");
        RequireFile(response, "roles/augeg4-engine/tasks/main.yml");
        RequireFile(response, "roles/augeg4-engine/handlers/main.yml");
        RequireFile(response, "roles/augeg4-engine/meta/main.yml");
        RequireFile(response, "README.md");
    }
}
