using System.Text;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

/// <summary>
/// Pure, no-I/O generator: turns one <see cref="ApplicationVersion"/>'s manifest
/// (<see cref="ConfigurationKey"/>s, <see cref="ApplicationUnitDefinition"/>s) into a one-shot
/// starting Ansible role/playbook scaffold — Jinja2 config templates plus a skeleton role and
/// playbook. This deliberately stops short of the parts that need real infrastructure judgement
/// (OS branching, package installs, firewall/reverse-proxy rules, real deployment paths) — those
/// stay as human-authored TODOs, consistent with the boundary already documented in
/// docs/application-configuration-model-analysis.md ("i file finali per istanza non devono
/// essere generati direttamente da Iris"). Naming (variable names, template targets, safe file
/// names) is shared with <see cref="GetApplicationInstallationAnsiblePlanHandler"/> via
/// <see cref="AnsibleNaming"/> so the two can never disagree for the same key.
/// </summary>
internal static class AnsibleScaffoldGenerator
{
    public static AnsibleScaffoldResponse Generate(
        ApplicationDefinition application,
        ApplicationVersion version,
        DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(version);

        var slug = application.Slug;
        var allKeys = version.ConfigurationKeys
            .OrderBy(key => key.TargetKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(key => key.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var fileTargets = new List<AnsibleTargetGroup>();
        var snippetTargets = new List<AnsibleTargetGroup>();
        var codeOnlyKeys = new List<ConfigurationKey>();

        foreach (var group in allKeys.GroupBy(key => key.TargetKind, StringComparer.OrdinalIgnoreCase))
        {
            var groupKeys = group.ToArray();
            switch (ClassifyTarget(group.Key))
            {
                case TargetCategory.CodeOnly:
                    codeOnlyKeys.AddRange(groupKeys);
                    break;
                case TargetCategory.Fragment:
                    snippetTargets.Add(new AnsibleTargetGroup(group.Key, groupKeys));
                    break;
                default:
                    fileTargets.Add(new AnsibleTargetGroup(group.Key, groupKeys));
                    break;
            }
        }

        var files = new List<AnsibleScaffoldFileResponse>();

        foreach (var group in fileTargets)
        {
            var fileName = AnsibleNaming.StripAnsiblePrefix(AnsibleNaming.ToTemplateTarget(group.TargetKind));
            var templateName = AnsibleNaming.ToJinjaTemplateName(AnsibleNaming.ToTemplateTarget(group.TargetKind));
            files.Add(new AnsibleScaffoldFileResponse(
                $"roles/{slug}/templates/{templateName}",
                $"Config template for '{fileName}'.",
                RenderConfigTemplate(application, version, group, fileName)));
        }

        foreach (var group in snippetTargets)
        {
            var (label, safeName) = ClassifyFragment(group.TargetKind);
            files.Add(new AnsibleScaffoldFileResponse(
                $"roles/{slug}/templates/snippets/{safeName}.j2",
                $"Snippet to paste into your {label} — not a whole file on its own.",
                RenderSnippet(application, version, group, label)));
        }

        files.Add(new AnsibleScaffoldFileResponse(
            $"roles/{slug}/defaults/main.yml",
            "Default values for every iris_* variable this role's templates/tasks may reference.",
            RenderDefaultsYml(allKeys)));
        files.Add(new AnsibleScaffoldFileResponse(
            $"roles/{slug}/tasks/main.yml",
            "Starting tasks: render config templates and manage the application's runtime unit(s).",
            RenderTasksYml(slug, fileTargets, snippetTargets, version)));
        files.Add(new AnsibleScaffoldFileResponse(
            $"roles/{slug}/handlers/main.yml",
            "Handler stub.",
            RenderHandlersYml(slug)));
        files.Add(new AnsibleScaffoldFileResponse(
            $"roles/{slug}/meta/main.yml",
            "Role metadata stub.",
            RenderMetaYml(application, version)));
        files.Add(new AnsibleScaffoldFileResponse(
            $"playbooks/{slug}.yml",
            "Starting playbook — point 'hosts' at your real inventory group before use.",
            RenderPlaybookYml(application, slug)));
        files.Add(new AnsibleScaffoldFileResponse(
            "README.md",
            "What this scaffold is and isn't.",
            RenderReadme(application, version, generatedAtUtc, fileTargets, snippetTargets, codeOnlyKeys)));

        return new AnsibleScaffoldResponse(slug, version.Version, generatedAtUtc, files);
    }

    private enum TargetCategory
    {
        /// <summary>A real deployable file — gets a full <c>.j2</c> template.</summary>
        File,

        /// <summary>Not a whole file — a snippet meant to be pasted into a Dockerfile/compose block.</summary>
        Fragment,

        /// <summary>Resolved directly by application code (env var read in code, etc.) — no template.</summary>
        CodeOnly
    }

    private sealed record AnsibleTargetGroup(string TargetKind, IReadOnlyList<ConfigurationKey> Keys);

    private static TargetCategory ClassifyTarget(string targetKind)
    {
        var trimmed = targetKind.Trim();
        if (trimmed.StartsWith("code:", StringComparison.OrdinalIgnoreCase))
        {
            return TargetCategory.CodeOnly;
        }

        if (trimmed.StartsWith("dockerfile:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("compose:", StringComparison.OrdinalIgnoreCase))
        {
            return TargetCategory.Fragment;
        }

        return TargetCategory.File;
    }

    private static (string Label, string SafeName) ClassifyFragment(string targetKind)
    {
        var trimmed = targetKind.Trim();
        if (trimmed.StartsWith("dockerfile:", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = trimmed["dockerfile:".Length..];
            var safe = AnsibleNaming.ToSafeFileName(string.IsNullOrWhiteSpace(suffix) ? "env" : suffix);
            return ("Dockerfile ENV block", $"dockerfile-{safe}");
        }

        var composeSuffix = trimmed["compose:".Length..];
        var composeSafe = AnsibleNaming.ToSafeFileName(string.IsNullOrWhiteSpace(composeSuffix) ? "environment" : composeSuffix);
        return ("docker-compose 'environment:' block", $"compose-{composeSafe}");
    }

    // --- Jinja2 config templates -------------------------------------------------------------

    private enum ConfigFormat { Flat, Json, ReferenceOnly }

    private static ConfigFormat ClassifyFormat(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        if (lower.Contains(".json", StringComparison.Ordinal))
        {
            return ConfigFormat.Json;
        }

        if (lower.Contains(".properties", StringComparison.Ordinal) ||
            lower.Contains(".env", StringComparison.Ordinal) ||
            !lower.Contains('.'))
        {
            return ConfigFormat.Flat;
        }

        // .xml, .config, .yml/.yaml and anything else structurally ambiguous: never guess at
        // real syntax — a confidently-wrong generated XML/config file is worse than an honest
        // reference list. See the format-selection rationale in the design notes above.
        return ConfigFormat.ReferenceOnly;
    }

    private static string RenderConfigTemplate(
        ApplicationDefinition application, ApplicationVersion version, AnsibleTargetGroup group, string fileName) =>
        ClassifyFormat(fileName) switch
        {
            ConfigFormat.Json => RenderHeaderComment(application, version, group.TargetKind) + RenderJsonBody(group),
            ConfigFormat.ReferenceOnly => RenderReferenceOnlyTemplate(application, version, group),
            _ => RenderHeaderComment(application, version, group.TargetKind) + RenderFlatBody(group)
        };

    private static string RenderHeaderComment(ApplicationDefinition application, ApplicationVersion version, string targetKind) =>
        "{#\n" +
        $"  Auto-generated by Iris from '{application.Name}' v{version.Version} — target '{targetKind}'.\n" +
        "  Starting point only: Iris will not overwrite this file once you've edited it. Regenerate\n" +
        "  (Applications > Generate Ansible scaffold) for a fresh diff against the current manifest.\n" +
        "#}\n";

    private static string RenderFlatBody(AnsibleTargetGroup group)
    {
        var sb = new StringBuilder();
        foreach (var key in group.Keys.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append("{# ").Append(DescribeKey(key)).AppendLine(" #}");
            var varName = AnsibleNaming.ToAnsibleVariableName(key.PlaceholderKey ?? key.Key);
            sb.Append(key.Key).Append("={{ ").Append(varName).AppendLine(" }}");
        }

        return sb.ToString();
    }

    private static string RenderJsonBody(AnsibleTargetGroup group)
    {
        var root = new JsonNode();
        foreach (var key in group.Keys)
        {
            var segments = key.Key.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            InsertJsonLeaf(root, segments.Length == 0 ? [key.Key] : segments, key);
        }

        return RenderJsonNode(root, 0) + "\n";
    }

    private sealed class JsonNode
    {
        public Dictionary<string, JsonNode> Children { get; } = new(StringComparer.Ordinal);

        public ConfigurationKey? Leaf { get; set; }
    }

    private static void InsertJsonLeaf(JsonNode root, IReadOnlyList<string> segments, ConfigurationKey key)
    {
        var node = root;
        foreach (var segment in segments)
        {
            if (!node.Children.TryGetValue(segment, out var child))
            {
                child = new JsonNode();
                node.Children[segment] = child;
            }

            node = child;
        }

        // Two different keys colliding on the same JSON path (one a leaf, one a prefix of
        // another) is a manifest ambiguity, not something worth resolving here — last write wins.
        node.Leaf = key;
    }

    private static string RenderJsonNode(JsonNode node, int indent)
    {
        if (node.Children.Count == 0)
        {
            return "{}";
        }

        var pad = new string(' ', (indent + 1) * 2);
        var closePad = new string(' ', indent * 2);
        var lines = node.Children
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => $"{pad}\"{EscapeJson(entry.Key)}\": {RenderJsonValue(entry.Value, indent + 1)}");
        return "{\n" + string.Join(",\n", lines) + "\n" + closePad + "}";
    }

    private static string RenderJsonValue(JsonNode node, int indent) =>
        node.Leaf is { } leaf ? RenderJsonLeafValue(leaf) : RenderJsonNode(node, indent);

    private static string RenderJsonLeafValue(ConfigurationKey key)
    {
        var varName = AnsibleNaming.ToAnsibleVariableName(key.PlaceholderKey ?? key.Key);
        return IsUnquotedJsonType(key.ValueType) ? $"{{{{ {varName} }}}}" : $"\"{{{{ {varName} }}}}\"";
    }

    private static bool IsUnquotedJsonType(string? valueType) =>
        valueType is not null &&
        (valueType.Equals("integer", StringComparison.OrdinalIgnoreCase) ||
         valueType.Equals("decimal", StringComparison.OrdinalIgnoreCase) ||
         valueType.Equals("boolean", StringComparison.OrdinalIgnoreCase));

    private static string EscapeJson(string value) => value.Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string RenderReferenceOnlyTemplate(ApplicationDefinition application, ApplicationVersion version, AnsibleTargetGroup group)
    {
        var sb = new StringBuilder();
        sb.Append("{#\n");
        sb.Append($"  Auto-generated reference for '{application.Name}' v{version.Version}, target '{group.TargetKind}'.\n");
        sb.Append("  Iris does not generate this format's real syntax — structured formats like this need\n");
        sb.Append("  real decisions (nesting, attributes vs elements, encoding) that a manifest-only\n");
        sb.Append("  generator would get wrong more often than right. Hand-author the real file using the\n");
        sb.Append("  keys below; this is a checklist, not a template to render as-is.\n");
        sb.Append("#}\n");
        foreach (var key in group.Keys.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            var varName = AnsibleNaming.ToAnsibleVariableName(key.PlaceholderKey ?? key.Key);
            sb.Append("{# ").Append(key.Key).Append(" -> ").Append(varName)
                .Append(" (").Append(DescribeKey(key)).AppendLine(") #}");
        }

        return sb.ToString();
    }

    private static string RenderSnippet(ApplicationDefinition application, ApplicationVersion version, AnsibleTargetGroup group, string label)
    {
        var sb = new StringBuilder();
        sb.Append("{#\n");
        sb.Append($"  Snippet for '{application.Name}' v{version.Version}, target '{group.TargetKind}' — paste this\n");
        sb.Append($"  into your {label}, it is not a whole file on its own. Starting point only; Iris will not\n");
        sb.Append("  overwrite your edits once you've committed this.\n");
        sb.Append("#}\n");
        foreach (var key in group.Keys.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            var varName = AnsibleNaming.ToAnsibleVariableName(key.PlaceholderKey ?? key.Key);
            sb.Append(key.Key).Append("={{ ").Append(varName).AppendLine(" }}");
        }

        return sb.ToString();
    }

    private static string DescribeKey(ConfigurationKey key)
    {
        var flags = new List<string> { key.Required ? "required" : "optional" };
        if (key.Secret)
        {
            flags.Add("secret");
        }

        var description = string.IsNullOrWhiteSpace(key.Description) ? null : key.Description;
        return description is null
            ? string.Join(", ", flags)
            : $"{string.Join(", ", flags)} — {description}";
    }

    // --- Role/playbook skeleton ---------------------------------------------------------------

    private static string RenderDefaultsYml(IReadOnlyList<ConfigurationKey> keys)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("# Every iris_* variable this role's templates/tasks may reference, generated from the Iris");
        sb.AppendLine("# manifest. Starting point only — Iris will not overwrite this file once you've edited it.");
        sb.AppendLine();

        if (keys.Count == 0)
        {
            sb.AppendLine("# No configuration keys were found in the manifest for this version.");
            return sb.ToString();
        }

        var seenVariableNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            var varName = AnsibleNaming.ToAnsibleVariableName(key.PlaceholderKey ?? key.Key);
            if (!seenVariableNames.Add(varName))
            {
                // Two keys sanitized to the same iris_* name — keep the first, skip the rest
                // rather than emit a literally-duplicate (and so invalid-by-convention) YAML key.
                continue;
            }

            var category = ClassifyTarget(key.TargetKind);
            var categoryNote = category == TargetCategory.CodeOnly
                ? "not file-backed — read directly by application code"
                : $"target: {key.TargetKind}";
            var description = string.IsNullOrWhiteSpace(key.Description) ? key.Key : key.Description;
            sb.Append(varName).Append(": ").Append(FormatDefaultsYmlValue(key))
                .Append("  # ").Append(DescribeKey(key))
                .Append(" | ").Append(categoryNote).Append(" | ").AppendLine(description);
        }

        return sb.ToString();
    }

    private static string FormatDefaultsYmlValue(ConfigurationKey key)
    {
        if (key.Secret)
        {
            return "\"\"";
        }

        if (!string.IsNullOrWhiteSpace(key.DefaultValue))
        {
            return FormatYamlScalar(key.DefaultValue, key.ValueType);
        }

        return key.ValueType?.Trim().ToLowerInvariant() switch
        {
            "integer" => "0",
            "decimal" => "0.0",
            "boolean" => "false",
            "array" => "[]",
            "json" => "{}",
            _ => "\"\""
        };
    }

    private static string FormatYamlScalar(string value, string? valueType)
    {
        var type = valueType?.Trim().ToLowerInvariant();
        return type is "integer" or "decimal" or "boolean" ? value : $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static string RenderTasksYml(
        string slug,
        IReadOnlyList<AnsibleTargetGroup> fileTargets,
        IReadOnlyList<AnsibleTargetGroup> snippetTargets,
        ApplicationVersion version)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("# Starting tasks generated by Iris. TODOs mark what Iris genuinely can't know (real");
        sb.AppendLine("# deployment paths, container images, service ExecStart/User) — fill those in by hand.");
        sb.AppendLine();

        foreach (var group in fileTargets)
        {
            var fileName = AnsibleNaming.StripAnsiblePrefix(AnsibleNaming.ToTemplateTarget(group.TargetKind));
            var templateName = AnsibleNaming.ToJinjaTemplateName(AnsibleNaming.ToTemplateTarget(group.TargetKind));
            sb.Append("- name: Render ").AppendLine(fileName);
            sb.AppendLine("  ansible.builtin.template:");
            sb.Append("    src: ").AppendLine(templateName);
            sb.Append("    dest: \"/opt/").Append(slug).Append('/').Append(fileName)
                .AppendLine("\"  # TODO: real deployment path — Iris does not know this");
            sb.AppendLine("  become: true");
            sb.AppendLine();
        }

        if (snippetTargets.Count > 0)
        {
            sb.AppendLine("# The following targets are snippets, not whole files (see roles/<slug>/templates/snippets/)");
            sb.AppendLine("# — paste them by hand into the right place (Dockerfile ENV / compose environment:);");
            sb.AppendLine("# there's no single templating task for a fragment.");
            sb.AppendLine();
        }

        var hasUnitTask = false;
        foreach (var unit in version.ApplicationUnits)
        {
            var executionTargets = AnsibleNaming.DeserializeList<string>(unit.ExecutionTargetsJson);
            var unitName = AnsibleNaming.ToSafeFileName(unit.Key);

            if (AnsibleNaming.SupportsDocker(executionTargets))
            {
                hasUnitTask = true;
                sb.Append("- name: Deploy container ").AppendLine(unit.Key);
                sb.AppendLine("  community.docker.docker_container:");
                sb.Append("    name: \"").Append(unitName).AppendLine("\"");
                sb.AppendLine("    image: \"{{ TODO_image }}\"  # TODO: real image reference — Iris does not know this");
                sb.AppendLine("    state: started");
                sb.AppendLine("  become: true");
                sb.AppendLine();
            }

            if (AnsibleNaming.SupportsService(executionTargets, unit.Kind))
            {
                hasUnitTask = true;
                sb.Append("- name: Manage service ").AppendLine(unit.Key);
                sb.AppendLine("  ansible.builtin.systemd_service:");
                sb.Append("    name: \"").Append(unitName)
                    .Append(".service\"  # TODO: author roles/").Append(slug).Append("/templates/systemd/")
                    .Append(unitName).AppendLine(".service.j2 by hand — Iris does not know the real ExecStart/User/WorkingDirectory");
                sb.AppendLine("    state: started");
                sb.AppendLine("    enabled: true");
                sb.AppendLine("  become: true");
                sb.AppendLine();
            }
        }

        if (fileTargets.Count == 0 && !hasUnitTask)
        {
            sb.AppendLine("# No file-backed configuration targets or application units were found in the manifest.");
        }

        return sb.ToString();
    }

    private static string RenderHandlersYml(string slug) =>
        "---\n" +
        "# Example — uncomment and adapt if your role restarts a service on template change.\n" +
        $"# - name: restart {slug}\n" +
        "#   ansible.builtin.systemd_service:\n" +
        $"#     name: {slug}.service\n" +
        "#     state: restarted\n";

    private static string RenderMetaYml(ApplicationDefinition application, ApplicationVersion version) =>
        "---\n" +
        "galaxy_info:\n" +
        $"  role_name: {application.Slug}\n" +
        $"  description: \"Iris-generated starting role for {application.Name} v{version.Version}. Edit freely — Iris will not overwrite this.\"\n" +
        "  author: Iris\n" +
        "  license: MIT\n" +
        "dependencies: []\n";

    private static string RenderPlaybookYml(ApplicationDefinition application, string slug) =>
        "---\n" +
        $"# Starting playbook for {application.Name}. Point 'hosts' at the real inventory group before use.\n" +
        $"- name: Deploy {application.Name}\n" +
        $"  hosts: {slug}  # TODO: real inventory group\n" +
        "  become: true\n" +
        "  roles:\n" +
        $"    - {slug}\n";

    private static string RenderReadme(
        ApplicationDefinition application,
        ApplicationVersion version,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<AnsibleTargetGroup> fileTargets,
        IReadOnlyList<AnsibleTargetGroup> snippetTargets,
        IReadOnlyList<ConfigurationKey> codeOnlyKeys)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {application.Name} — Ansible scaffold");
        sb.AppendLine();
        sb.AppendLine($"Generated by Iris on {generatedAtUtc:u} from '{application.Slug}' v{version.Version}.");
        sb.AppendLine();
        sb.AppendLine("This is a **starting point**, not a managed artifact: Iris will never overwrite these files");
        sb.AppendLine("once you've committed and edited them. Regenerate (Applications > Generate Ansible scaffold)");
        sb.AppendLine("only when you want a fresh reference diff against the current manifest — re-running it does");
        sb.AppendLine("not touch anything you've already committed elsewhere.");
        sb.AppendLine();
        sb.AppendLine("## What Iris generated");
        sb.AppendLine();
        sb.AppendLine($"- `roles/{application.Slug}/templates/*.j2` — {fileTargets.Count} config template(s), one per target the manifest declares.");
        if (snippetTargets.Count > 0)
        {
            sb.AppendLine($"- `roles/{application.Slug}/templates/snippets/*.j2` — {snippetTargets.Count} Dockerfile/compose snippet(s), not whole files.");
        }

        sb.AppendLine($"- `roles/{application.Slug}/defaults/main.yml` — every `iris_*` variable this role may reference.");
        sb.AppendLine($"- `roles/{application.Slug}/tasks/main.yml` — a starting task list; TODOs mark what Iris can't know");
        sb.AppendLine("  (real deployment paths, container images, service ExecStart/User/WorkingDirectory).");
        sb.AppendLine($"- `roles/{application.Slug}/handlers/main.yml`, `roles/{application.Slug}/meta/main.yml` — stubs.");
        sb.AppendLine($"- `playbooks/{application.Slug}.yml` — a starting playbook; point `hosts` at your real inventory group.");
        sb.AppendLine();
        sb.AppendLine("## Not generated — genuinely outside Iris's scope");
        sb.AppendLine();
        sb.AppendLine("- Firewall/reverse-proxy/TLS/DNS rules, OS package installation.");
        sb.AppendLine("- Service `ExecStart`/`User`/`WorkingDirectory` specifics, real deployment paths, container images.");
        if (codeOnlyKeys.Count > 0)
        {
            sb.AppendLine("- These keys are consumed directly by application code, not by any file Iris can template:");
            foreach (var key in codeOnlyKeys.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var varName = AnsibleNaming.ToAnsibleVariableName(key.PlaceholderKey ?? key.Key);
                sb.Append("  - `").Append(key.Key).Append("` (`").Append(varName).AppendLine("`)");
            }
        }

        return sb.ToString();
    }
}
