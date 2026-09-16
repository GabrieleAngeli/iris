using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

public sealed record CancelPreparedActionCommand(Guid PreparedActionId, string? Reason);

/// <summary>The operator declined a prepared action instead of confirming it. No-op guarded on the
/// domain side; here we surface a clear error instead if it was already executed/canceled.</summary>
public sealed class CancelPreparedActionHandler(
    IPreparedActionRepository actions,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<PreparedActionResponse> HandleAsync(
        CancelPreparedActionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var action = await actions.GetForUpdateAsync(command.PreparedActionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Prepared action", command.PreparedActionId);
        if (action.Status != PreparedActionStatus.Prepared)
        {
            throw new ValidationException(
                $"This action is already {action.Status.ToString().ToLowerInvariant()} and cannot be canceled.");
        }

        action.Cancel(command.Reason, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return action.ToResponse(action.DeserializePlan(), action.DeserializeValidation(), run: null);
    }
}
