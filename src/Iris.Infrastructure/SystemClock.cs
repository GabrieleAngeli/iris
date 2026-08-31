using Iris.Application.Abstractions;

namespace Iris.Infrastructure;

/// <summary>Default <see cref="IClock"/> backed by the system clock.</summary>
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
