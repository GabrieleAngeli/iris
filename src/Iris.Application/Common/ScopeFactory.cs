using Iris.Domain.Access;

namespace Iris.Application.Common;

/// <summary>Translates request-shaped scope data into an <see cref="AccessScope"/>.</summary>
public static class ScopeFactory
{
    /// <summary>From the optional customer/context pair carried on a query.</summary>
    public static AccessScope From(Guid? customerId, Guid? contextId)
    {
        if (customerId is null && contextId is null)
        {
            return AccessScope.Global();
        }

        if (customerId is null)
        {
            throw new InvalidScopeRequestException("A context scope requires the owning customer id.");
        }

        return contextId is null
            ? AccessScope.ForCustomer(customerId.Value)
            : AccessScope.ForContext(customerId.Value, contextId.Value);
    }

    /// <summary>From an explicit scope-type string plus its ids (used by role-assignment commands).</summary>
    public static AccessScope FromParts(string scopeType, Guid? customerId, Guid? contextId)
    {
        if (!Enum.TryParse<ScopeType>(scopeType, ignoreCase: true, out var parsed))
        {
            throw new ValidationException(
                $"Unknown scope type '{scopeType}'. Expected Global, Customer or Context.");
        }

        return parsed switch
        {
            ScopeType.Global => AccessScope.Global(),
            ScopeType.Customer => customerId is { } c
                ? AccessScope.ForCustomer(c)
                : throw new ValidationException("A customer scope requires customerId."),
            ScopeType.Context => customerId is { } cc && contextId is { } ctx
                ? AccessScope.ForContext(cc, ctx)
                : throw new ValidationException("A context scope requires customerId and contextId."),
            _ => throw new ValidationException($"Unsupported scope type '{scopeType}'."),
        };
    }
}
