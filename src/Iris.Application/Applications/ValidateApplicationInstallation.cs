using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Applications;

public sealed record ValidateApplicationInstallationQuery(Guid InstallationId);

/// <summary>
/// The deployment Validation Engine. Given an <see cref="ApplicationInstallation"/> it
/// compares the release's configuration knowledge (<see cref="ApplicationVersion"/>:
/// placeholders, configuration keys, dependencies, runtime metadata, dependency
/// constraints) against the concrete target (<see cref="ServerNode"/> capabilities,
/// resources and used ports, plus any bound <see cref="DataServiceInstance"/>) and
/// returns a typed list of checks with severity. It reads only — it never mutates the
/// installation or launches anything.
/// </summary>
public sealed class ValidateApplicationInstallationHandler(
    IApplicationInstallationRepository installations,
    IApplicationRepository applications,
    IServerRepository servers,
    IDataServiceRepository dataServices)
{
    public async Task<ApplicationInstallationValidationResponse> HandleAsync(
        ValidateApplicationInstallationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var installation = await installations.GetAsync(query.InstallationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Application installation", query.InstallationId);
        var application = await applications.GetAsync(installation.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Application", installation.ApplicationId);
        var version = application.Versions.SingleOrDefault(v => v.Id == installation.ApplicationVersionId)
            ?? throw new NotFoundException("Application version", installation.ApplicationVersionId);
        var server = await servers.GetAsync(installation.ServerNodeId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Server", installation.ServerNodeId);
        var catalog = await applications.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var allDataServices = await dataServices.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var checks = new List<ApplicationInstallationValidationCheckResponse>();
        var profile = installation.InstallationProfileKey;

        var bindingsByKey = installation.Bindings
            .GroupBy(binding => binding.PlaceholderKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var resolvedKeys = bindingsByKey
            .Where(pair => IsResolved(pair.Value))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ValidatePlaceholders(version, bindingsByKey, resolvedKeys, checks);
        ValidateConfigurationKeys(version, profile, resolvedKeys, checks);
        ValidateDependencies(version, catalog, resolvedKeys, checks);
        ValidateOperatingSystem(version, server, checks);
        ValidateCapabilities(version, server, checks);
        ValidatePorts(version, server, checks);
        ValidateCapacity(version, server, checks);
        ValidateDependencyConstraints(version, installation, allDataServices, checks);

        var errors = checks.Count(check => check.Severity == Severity.Error);
        var warnings = checks.Count(check => check.Severity == Severity.Warning);
        var infos = checks.Count(check => check.Severity == Severity.Info);

        return new ApplicationInstallationValidationResponse(
            installation.Id,
            installation.Name,
            application.Slug,
            version.Version,
            installation.ApplicationUnitKey,
            installation.InstallationProfileKey,
            server.Name,
            installation.Environment.ToString(),
            errors == 0,
            errors,
            warnings,
            infos,
            checks
                .OrderBy(check => SeverityRank(check.Severity))
                .ThenBy(check => check.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(check => check.Target, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static bool IsResolved(ApplicationInstallationBinding binding) =>
        binding.TargetId is not null ||
        !string.IsNullOrWhiteSpace(binding.TargetSlug) ||
        (string.Equals(binding.TargetKind, ApplicationInstallationTargetKinds.Manual, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(binding.ValuePreview));

    private static void ValidatePlaceholders(
        ApplicationVersion version,
        IReadOnlyDictionary<string, ApplicationInstallationBinding> bindingsByKey,
        IReadOnlySet<string> resolvedKeys,
        List<ApplicationInstallationValidationCheckResponse> checks)
    {
        foreach (var placeholder in version.Placeholders.Where(placeholder => placeholder.Required))
        {
            if (resolvedKeys.Contains(placeholder.Key))
            {
                continue;
            }

            checks.Add(bindingsByKey.ContainsKey(placeholder.Key)
                ? new ApplicationInstallationValidationCheckResponse(
                    "placeholder.unresolved",
                    Severity.Error,
                    Category.Placeholder,
                    placeholder.Key,
                    $"Placeholder '{placeholder.Key}' has a binding but no concrete target selected.")
                : new ApplicationInstallationValidationCheckResponse(
                    "placeholder.unbound",
                    Severity.Error,
                    Category.Placeholder,
                    placeholder.Key,
                    $"Required placeholder '{placeholder.Key}' is not bound to any target."));
        }
    }

    private static void ValidateConfigurationKeys(
        ApplicationVersion version,
        string? profile,
        IReadOnlySet<string> resolvedKeys,
        List<ApplicationInstallationValidationCheckResponse> checks)
    {
        foreach (var key in version.ConfigurationKeys)
        {
            if (!key.Required || !IsInSelectedProfile(key.ProfilesJson, profile))
            {
                continue;
            }

            var hasDefault = !string.IsNullOrWhiteSpace(key.DefaultValue);

            if (!string.IsNullOrWhiteSpace(key.PlaceholderKey))
            {
                if (resolvedKeys.Contains(key.PlaceholderKey) || hasDefault)
                {
                    continue;
                }

                checks.Add(key.Secret
                    ? new ApplicationInstallationValidationCheckResponse(
                        "configuration.secret-unbound",
                        Severity.Error,
                        Category.Configuration,
                        key.Key,
                        $"Required secret key '{key.Key}' must be bound (placeholder '{key.PlaceholderKey}') to a secret target.")
                    : new ApplicationInstallationValidationCheckResponse(
                        "configuration.unresolved",
                        Severity.Error,
                        Category.Configuration,
                        key.Key,
                        $"Required key '{key.Key}' has neither a binding for placeholder '{key.PlaceholderKey}' nor a default value."));
                continue;
            }

            if (hasDefault)
            {
                continue;
            }

            checks.Add(key.Secret
                ? new ApplicationInstallationValidationCheckResponse(
                    "configuration.secret-missing",
                    Severity.Error,
                    Category.Configuration,
                    key.Key,
                    $"Required secret key '{key.Key}' has no placeholder and no default; it cannot be resolved at deploy time.")
                : new ApplicationInstallationValidationCheckResponse(
                    "configuration.manual-value",
                    Severity.Warning,
                    Category.Configuration,
                    key.Key,
                    $"Required key '{key.Key}' has no default; an operator must supply a value for this installation."));
        }
    }

    private static void ValidateDependencies(
        ApplicationVersion version,
        IReadOnlyList<ApplicationDefinition> catalog,
        IReadOnlySet<string> resolvedKeys,
        List<ApplicationInstallationValidationCheckResponse> checks)
    {
        foreach (var dependency in version.Dependencies)
        {
            var bound = !string.IsNullOrWhiteSpace(dependency.PlaceholderKey) &&
                resolvedKeys.Contains(dependency.PlaceholderKey);

            if (dependency.Required && !string.IsNullOrWhiteSpace(dependency.PlaceholderKey) && !bound)
            {
                checks.Add(new ApplicationInstallationValidationCheckResponse(
                    "dependency.unbound",
                    Severity.Error,
                    Category.Dependency,
                    dependency.Name,
                    $"Required {dependency.Category} dependency '{dependency.Name}' (placeholder '{dependency.PlaceholderKey}') is not bound."));
            }
            else if (!dependency.Required && !string.IsNullOrWhiteSpace(dependency.PlaceholderKey) && !bound)
            {
                checks.Add(new ApplicationInstallationValidationCheckResponse(
                    "dependency.optional-unbound",
                    Severity.Info,
                    Category.Dependency,
                    dependency.Name,
                    $"Optional {dependency.Category} dependency '{dependency.Name}' is not bound."));
            }

            if (!string.IsNullOrWhiteSpace(dependency.ProviderApplicationSlug) &&
                !catalog.Any(app => string.Equals(app.Slug, dependency.ProviderApplicationSlug, StringComparison.OrdinalIgnoreCase)))
            {
                checks.Add(new ApplicationInstallationValidationCheckResponse(
                    "dependency.provider-missing",
                    Severity.Warning,
                    Category.Dependency,
                    dependency.ProviderApplicationSlug,
                    $"Dependency '{dependency.Name}' points to provider application '{dependency.ProviderApplicationSlug}', which is not in the Iris catalog."));
            }
        }
    }

    private static void ValidateOperatingSystem(
        ApplicationVersion version,
        ServerNode server,
        List<ApplicationInstallationValidationCheckResponse> checks)
    {
        var osSupport = DeserializeList<RuntimeOsSupportInfo>(version.RuntimeMetadata.OsSupportJson);
        if (osSupport.Count > 0)
        {
            var matches = osSupport.Any(entry =>
                OsMatches(entry.Type, server.Os) || OsMatches(entry.Distribution, server.Os));
            if (!matches)
            {
                var tested = string.Join(", ", osSupport.Select(entry => entry.Type).Where(type => !string.IsNullOrWhiteSpace(type)));
                checks.Add(new ApplicationInstallationValidationCheckResponse(
                    "os.incompatible",
                    Severity.Error,
                    Category.OperatingSystem,
                    server.Os.ToString(),
                    $"Version '{version.Version}' is tested on [{tested}] but the target server runs {server.Os}."));
            }

            return;
        }

        if (version.RuntimeMetadata.PreferredOs is { } preferred && preferred != server.Os)
        {
            checks.Add(new ApplicationInstallationValidationCheckResponse(
                "os.not-preferred",
                Severity.Warning,
                Category.OperatingSystem,
                server.Os.ToString(),
                $"Version '{version.Version}' prefers {preferred} but the target server runs {server.Os}."));
        }
    }

    private static void ValidateCapabilities(
        ApplicationVersion version,
        ServerNode server,
        List<ApplicationInstallationValidationCheckResponse> checks)
    {
        var portKeys = DeserializeList<string>(version.RuntimeMetadata.PortKeysJson);
        var needsServiceHost = version.RuntimeMetadata.RequiredPorts.Count > 0 ||
            portKeys.Count > 0 ||
            version.ApplicationUnits.Count > 0;
        if (!needsServiceHost)
        {
            return;
        }

        if (server.Capabilities.Count == 0)
        {
            checks.Add(new ApplicationInstallationValidationCheckResponse(
                "capability.unknown",
                Severity.Info,
                Category.Capability,
                nameof(NodeCapability.ServiceHost),
                $"Server '{server.Name}' has no declared capabilities; cannot confirm it can host services."));
            return;
        }

        if (!server.Capabilities.Contains(NodeCapability.ServiceHost))
        {
            checks.Add(new ApplicationInstallationValidationCheckResponse(
                "capability.missing",
                Severity.Error,
                Category.Capability,
                nameof(NodeCapability.ServiceHost),
                $"Server '{server.Name}' does not declare the {nameof(NodeCapability.ServiceHost)} capability required to run this application."));
        }
    }

    private static void ValidatePorts(
        ApplicationVersion version,
        ServerNode server,
        List<ApplicationInstallationValidationCheckResponse> checks)
    {
        foreach (var port in version.RuntimeMetadata.RequiredPorts.Intersect(server.UsedPorts).OrderBy(port => port))
        {
            checks.Add(new ApplicationInstallationValidationCheckResponse(
                "port.collision",
                Severity.Error,
                Category.Port,
                port.ToString(CultureInfo.InvariantCulture),
                $"Port {port} required by this application is already in use on server '{server.Name}'."));
        }
    }

    private static void ValidateCapacity(
        ApplicationVersion version,
        ServerNode server,
        List<ApplicationInstallationValidationCheckResponse> checks)
    {
        var resources = server.Resources;
        if (resources is null)
        {
            checks.Add(new ApplicationInstallationValidationCheckResponse(
                "capacity.unknown",
                Severity.Info,
                Category.Capacity,
                server.Name,
                $"Server '{server.Name}' has no resource profile; capacity cannot be checked."));
            return;
        }

        CheckResource(
            "cpu",
            "CPU cores",
            resources.CpuCores,
            version.RuntimeMetadata.MinimumCpuCores,
            version.RuntimeMetadata.RequiredCpuCores,
            checks,
            server.Name);
        CheckResource(
            "memory",
            "memory (MB)",
            resources.MemoryMb,
            version.RuntimeMetadata.MinimumMemoryMb,
            version.RuntimeMetadata.RequiredMemoryMb,
            checks,
            server.Name);
    }

    private static void CheckResource(
        string target,
        string label,
        int? available,
        int? minimum,
        int? recommended,
        List<ApplicationInstallationValidationCheckResponse> checks,
        string serverName)
    {
        if (available is not { } have)
        {
            return;
        }

        if (minimum is { } min && have < min)
        {
            checks.Add(new ApplicationInstallationValidationCheckResponse(
                $"capacity.{target}",
                Severity.Error,
                Category.Capacity,
                target,
                $"Application needs at least {min} {label}; server '{serverName}' has {have}."));
            return;
        }

        if (recommended is { } rec && have < rec)
        {
            checks.Add(new ApplicationInstallationValidationCheckResponse(
                $"capacity.{target}-recommended",
                Severity.Warning,
                Category.Capacity,
                target,
                $"Application recommends {rec} {label}; server '{serverName}' has {have}."));
        }
    }

    private static void ValidateDependencyConstraints(
        ApplicationVersion version,
        ApplicationInstallation installation,
        IReadOnlyList<DataServiceInstance> dataServices,
        List<ApplicationInstallationValidationCheckResponse> checks)
    {
        foreach (var constraint in version.DependencyConstraints)
        {
            if (string.IsNullOrWhiteSpace(constraint.PlaceholderKey))
            {
                continue;
            }

            var binding = installation.Bindings.FirstOrDefault(candidate =>
                string.Equals(candidate.PlaceholderKey, constraint.PlaceholderKey, StringComparison.OrdinalIgnoreCase) &&
                IsResolved(candidate));
            if (binding is null)
            {
                continue;
            }

            var label = constraint.ServiceKind ?? constraint.PlaceholderKey;

            if (string.Equals(binding.TargetKind, ApplicationInstallationTargetKinds.Application, StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(new ApplicationInstallationValidationCheckResponse(
                    "constraint.application-target",
                    Severity.Info,
                    Category.Constraint,
                    label,
                    $"Version constraint '{constraint.VersionExpression ?? constraint.ServiceKind}' on '{constraint.PlaceholderKey}' targets a provider application and is not verified in this version."));
                continue;
            }

            if (!string.Equals(binding.TargetKind, ApplicationInstallationTargetKinds.DataService, StringComparison.OrdinalIgnoreCase) ||
                binding.TargetId is not { } dataServiceId)
            {
                continue;
            }

            var dataService = dataServices.FirstOrDefault(instance => instance.Id == dataServiceId);
            if (dataService is null)
            {
                checks.Add(new ApplicationInstallationValidationCheckResponse(
                    "constraint.target-missing",
                    Severity.Info,
                    Category.Constraint,
                    label,
                    $"Constraint on '{constraint.PlaceholderKey}' cannot be checked: the bound data service no longer exists."));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(constraint.ServiceKind))
            {
                var expected = MapServiceKind(constraint.ServiceKind);
                if (expected is null)
                {
                    checks.Add(new ApplicationInstallationValidationCheckResponse(
                        "constraint.unverifiable-kind",
                        Severity.Info,
                        Category.Constraint,
                        label,
                        $"Constraint requires service kind '{constraint.ServiceKind}', which Iris does not manage as a data service; not verified."));
                }
                else if (expected != dataService.Kind)
                {
                    checks.Add(new ApplicationInstallationValidationCheckResponse(
                        "constraint.service-kind",
                        Severity.Error,
                        Category.Constraint,
                        label,
                        $"Constraint on '{constraint.PlaceholderKey}' requires a {expected} service but the bound data service '{dataService.Name}' is {dataService.Kind}."));
                }
            }

            if (!string.IsNullOrWhiteSpace(constraint.VersionExpression))
            {
                var satisfied = SatisfiesVersion(constraint.VersionExpression, dataService.Version);
                if (satisfied is false)
                {
                    checks.Add(new ApplicationInstallationValidationCheckResponse(
                        "constraint.version",
                        Severity.Error,
                        Category.Constraint,
                        label,
                        $"Bound data service '{dataService.Name}' version '{dataService.Version}' does not satisfy '{constraint.VersionExpression}'."));
                }
                else if (satisfied is null)
                {
                    checks.Add(new ApplicationInstallationValidationCheckResponse(
                        "constraint.unverifiable-version",
                        Severity.Info,
                        Category.Constraint,
                        label,
                        $"Constraint '{constraint.VersionExpression}' on '{constraint.PlaceholderKey}' could not be evaluated against version '{dataService.Version}'."));
                }
            }
        }
    }

    private static bool IsInSelectedProfile(string? profilesJson, string? selectedProfile)
    {
        if (string.IsNullOrWhiteSpace(selectedProfile) || string.IsNullOrWhiteSpace(profilesJson))
        {
            return true;
        }

        try
        {
            var profiles = JsonSerializer.Deserialize<IReadOnlyList<string>>(profilesJson) ?? [];
            return profiles.Count == 0 || profiles.Contains(selectedProfile, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool OsMatches(string? token, ServerOs os)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var value = token.Trim().ToLowerInvariant();
        return os switch
        {
            ServerOs.Windows => value.Contains("win"),
            ServerOs.Linux => value.Contains("linux") || value.Contains("unix"),
            _ => value.Contains(os.ToString().ToLowerInvariant()),
        };
    }

    private static DataServiceKind? MapServiceKind(string serviceKind)
    {
        var value = serviceKind.Trim().ToLowerInvariant();
        if (value.Contains("postgres") || value.Contains("pgsql"))
        {
            return DataServiceKind.PostgreSql;
        }

        if (value.Contains("redis"))
        {
            return DataServiceKind.Redis;
        }

        if (value.Contains("mssql") || value.Contains("sqlserver") || value.Contains("sql server") || value.Contains("sql-server"))
        {
            return DataServiceKind.Mssql;
        }

        return null;
    }

    /// <summary>
    /// Evaluates a small subset of version expressions against an actual version:
    /// bare token (major/exact match), <c>== >= &gt; &lt;= &lt;</c> comparisons and inclusive
    /// <c>A-B</c> ranges, joined by <c>&amp;&amp;</c>/<c>,</c>/<c>and</c>. Returns <c>null</c>
    /// when the expression or the actual version cannot be parsed — the caller then reports
    /// an <c>info</c> check rather than blocking.
    /// </summary>
    internal static bool? SatisfiesVersion(string? expression, string? actualVersion)
    {
        if (string.IsNullOrWhiteSpace(expression) || TryParseVersion(actualVersion) is not { } actual)
        {
            return null;
        }

        var clauses = Regex.Split(expression, @"\s*(?:&&|,|\band\b)\s*", RegexOptions.IgnoreCase)
            .Select(clause => clause.Trim())
            .Where(clause => clause.Length > 0)
            .ToArray();
        if (clauses.Length == 0)
        {
            return null;
        }

        var result = true;
        foreach (var clause in clauses)
        {
            var evaluated = EvaluateClause(clause, actual, actualVersion!);
            if (evaluated is null)
            {
                return null;
            }

            result &= evaluated.Value;
        }

        return result;
    }

    private static bool? EvaluateClause(string clause, Version actual, string actualRaw)
    {
        var rangeMatch = Regex.Match(clause, @"^v?(\d+(?:\.\d+)*)\s*-\s*v?(\d+(?:\.\d+)*)$");
        if (rangeMatch.Success)
        {
            if (TryParseVersion(rangeMatch.Groups[1].Value) is not { } low ||
                TryParseVersion(rangeMatch.Groups[2].Value) is not { } high)
            {
                return null;
            }

            return actual >= low && actual <= high;
        }

        var opMatch = Regex.Match(clause, @"^(>=|<=|==|=|>|<|~>|\^)?\s*v?(\d+(?:\.\d+)*)$");
        if (!opMatch.Success)
        {
            return null;
        }

        var op = opMatch.Groups[1].Value;
        var operand = opMatch.Groups[2].Value;
        if (TryParseVersion(operand) is not { } target)
        {
            return null;
        }

        // A bare major token ("6") means "same major".
        if (string.IsNullOrEmpty(op))
        {
            return operand.Contains('.') ? actual == target : actual.Major == target.Major;
        }

        return op switch
        {
            ">=" => actual >= target,
            "<=" => actual <= target,
            ">" => actual > target,
            "<" => actual < target,
            "==" or "=" => operand.Contains('.') ? actual == target : actual.Major == target.Major,
            "~>" or "^" => actual >= target && actual.Major == target.Major,
            _ => null,
        };
    }

    private static Version? TryParseVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var match = Regex.Match(raw.Trim(), @"\d+(?:\.\d+){0,3}");
        if (!match.Success)
        {
            return null;
        }

        var parts = match.Value.Split('.');
        var normalized = parts.Length switch
        {
            1 => $"{parts[0]}.0",
            _ => match.Value,
        };
        return Version.TryParse(normalized, out var version) ? version : null;
    }

    private static IReadOnlyList<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<T>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static int SeverityRank(string severity) => severity switch
    {
        Severity.Error => 0,
        Severity.Warning => 1,
        _ => 2,
    };

    private static class Severity
    {
        public const string Error = "error";
        public const string Warning = "warning";
        public const string Info = "info";
    }

    private static class Category
    {
        public const string Placeholder = "placeholder";
        public const string Configuration = "configuration";
        public const string Dependency = "dependency";
        public const string OperatingSystem = "os";
        public const string Capability = "capability";
        public const string Port = "port";
        public const string Capacity = "capacity";
        public const string Constraint = "constraint";
    }
}
