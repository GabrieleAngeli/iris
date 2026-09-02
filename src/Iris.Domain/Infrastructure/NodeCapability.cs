namespace Iris.Domain.Infrastructure;

/// <summary>
/// A functional role a <see cref="ServerNode"/> can play. A server carries zero or more of
/// these via <see cref="ServerNode.Capabilities"/> — a plain collection, not <c>[Flags]</c>.
/// </summary>
public enum NodeCapability
{
    LoadBalancer = 0,
    Database = 1,
    ServiceHost = 2,
    Presentation = 3,
}
