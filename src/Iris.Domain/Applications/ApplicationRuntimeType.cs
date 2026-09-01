namespace Iris.Domain.Applications;

/// <summary>Technology stack an <see cref="ApplicationDefinition"/> is built with.</summary>
public enum ApplicationRuntimeType
{
    CSharp = 0,
    JavaScript = 1,
    Java = 2,
    Node = 3,
    Docker = 4,
}
