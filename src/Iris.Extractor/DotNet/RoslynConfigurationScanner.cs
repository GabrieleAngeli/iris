using Iris.Contracts.Applications;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Iris.Extractor.DotNet;

/// <summary>Syntax-only static analysis (no MSBuild/semantic compilation of the target project — that
/// would require restoring an arbitrary external repo, which is out of scope for a fast, dependency-free
/// scan) that finds <c>IConfiguration</c> usages in code: keys read via <c>GetValue</c>/<c>GetSection</c>/
/// the indexer, and connection strings read via <c>GetConnectionString</c>. Catches keys the app expects
/// that aren't declared in any <c>appsettings.json</c> (e.g. environment-only values).</summary>
internal static class RoslynConfigurationScanner
{
    public static ConfigurationFragment Scan(string root)
    {
        var configurationKeys = new Dictionary<string, ConfigurationKeyInput>(StringComparer.OrdinalIgnoreCase);
        var dependencies = new Dictionary<string, DependencyInput>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in FindSourceFiles(root))
        {
            var walker = new ConfigurationUsageWalker();
            walker.Visit(CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetRoot());

            var fileName = Path.GetFileName(file);

            foreach (var key in walker.DiscoveredKeys)
            {
                configurationKeys.TryAdd(key, new ConfigurationKeyInput(
                    key,
                    TargetKind: "code:IConfiguration",
                    Required: false,
                    Secret: SecretHeuristics.LooksSecret(key),
                    DefaultValue: null,
                    Description: $"Referenced in {fileName}.",
                    Purpose: null,
                    PlaceholderKey: null));
            }

            foreach (var name in walker.DiscoveredConnectionStrings)
            {
                var key = $"ConnectionStrings:{name}";
                configurationKeys.TryAdd(key, new ConfigurationKeyInput(
                    key,
                    TargetKind: "code:IConfiguration",
                    Required: false,
                    Secret: true,
                    DefaultValue: null,
                    Description: $"Referenced via GetConnectionString in {fileName}.",
                    Purpose: null,
                    PlaceholderKey: null));

                dependencies.TryAdd(name, new DependencyInput(
                    name,
                    Category: "database",
                    Required: false,
                    Description: $"Referenced via GetConnectionString in {fileName}.",
                    PlaceholderKey: null));
            }
        }

        return new ConfigurationFragment(configurationKeys.Values.ToArray(), dependencies.Values.ToArray());
    }

    private static IEnumerable<string> FindSourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !PathFiltering.IsExcluded(root, path));

    private sealed class ConfigurationUsageWalker : CSharpSyntaxWalker
    {
        public List<string> DiscoveredKeys { get; } = [];

        public List<string> DiscoveredConnectionStrings { get; } = [];

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax memberAccess &&
                LooksLikeConfigurationReceiver(memberAccess.Expression) &&
                TryGetFirstStringArgument(node.ArgumentList, out var key))
            {
                switch (memberAccess.Name.Identifier.Text)
                {
                    case "GetConnectionString":
                        DiscoveredConnectionStrings.Add(key);
                        break;
                    case "GetValue" or "GetSection" or "GetRequiredSection":
                        DiscoveredKeys.Add(key);
                        break;
                }
            }

            base.VisitInvocationExpression(node);
        }

        public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            if (LooksLikeConfigurationReceiver(node.Expression) &&
                node.ArgumentList.Arguments.Count == 1 &&
                TryGetStringLiteral(node.ArgumentList.Arguments[0].Expression, out var key))
            {
                DiscoveredKeys.Add(key);
            }

            base.VisitElementAccessExpression(node);
        }

        private static bool LooksLikeConfigurationReceiver(ExpressionSyntax expression)
        {
            var name = expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
                _ => null,
            };

            return name is not null && name.Contains("config", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetFirstStringArgument(ArgumentListSyntax? argumentList, out string value)
        {
            var first = argumentList?.Arguments.FirstOrDefault();
            if (first is null)
            {
                value = string.Empty;
                return false;
            }

            return TryGetStringLiteral(first.Expression, out value);
        }

        private static bool TryGetStringLiteral(ExpressionSyntax expression, out string value)
        {
            if (expression is LiteralExpressionSyntax { Token.Value: string literalValue })
            {
                value = literalValue;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}
