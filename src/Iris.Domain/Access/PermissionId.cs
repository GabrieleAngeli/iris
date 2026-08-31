using System.Text.RegularExpressions;

namespace Iris.Domain.Access;

/// <summary>
/// A single fine-grained permission, identified by a dotted lowercase code such
/// as <c>infrastructure.read</c> or <c>deployments.prepare</c>.
/// </summary>
public readonly partial struct PermissionId : IEquatable<PermissionId>
{
    private PermissionId(string value) => Value = value;

    public string Value { get; }

    public static PermissionId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim().ToLowerInvariant();
        if (!CodePattern().IsMatch(normalized))
        {
            throw new FormatException(
                $"'{value}' is not a valid permission code (expected dotted segments, e.g. 'area.action').");
        }

        return new PermissionId(normalized);
    }

    public bool Equals(PermissionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is PermissionId other && Equals(other);

    public override int GetHashCode() => Value is null ? 0 : Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value ?? string.Empty;

    public static bool operator ==(PermissionId left, PermissionId right) => left.Equals(right);

    public static bool operator !=(PermissionId left, PermissionId right) => !left.Equals(right);

    [GeneratedRegex("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9]*)+$")]
    private static partial Regex CodePattern();
}
