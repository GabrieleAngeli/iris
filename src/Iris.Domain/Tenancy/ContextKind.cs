namespace Iris.Domain.Tenancy;

/// <summary>Lifecycle stage a <see cref="CustomerContext"/> represents.</summary>
public enum ContextKind
{
    Test = 0,
    Staging = 1,
    Production = 2,
}
