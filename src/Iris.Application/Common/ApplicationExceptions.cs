namespace Iris.Application.Common;

/// <summary>A referenced aggregate does not exist. Surfaced as HTTP 404.</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.");

/// <summary>The request conflicts with the current state (duplicate key, existing assignment…). HTTP 409.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>The caller is authenticated, but cannot perform this operation. HTTP 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>The request is well-formed but semantically invalid. HTTP 400.</summary>
public sealed class ValidationException(string message) : Exception(message);
