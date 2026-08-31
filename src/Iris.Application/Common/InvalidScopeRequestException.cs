namespace Iris.Application.Common;

/// <summary>Raised when a caller asks for a scope that cannot be formed (e.g. a context without its customer).</summary>
public sealed class InvalidScopeRequestException(string message) : Exception(message);
