namespace Iris.Domain.Infrastructure;

/// <summary>Where a <see cref="ServerNode"/> physically runs.</summary>
public enum ServerHostingType
{
    SelfHosted = 0,
    Cloud = 1,
}
