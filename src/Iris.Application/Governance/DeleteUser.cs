using Iris.Application.Abstractions;
using Iris.Application.Common;

namespace Iris.Application.Governance;

/// <summary>Command for <c>DELETE /governance/users/{id}</c> — removes the user and their role assignments.</summary>
public sealed record DeleteUserCommand(Guid Id);

public sealed class DeleteUserHandler(IUserRepository users, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(DeleteUserCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.GetAsync(command.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("User", command.Id);

        // Role assignments are removed by the cascade FK; server credentials keep a plain
        // OwnerUserId that will simply stop resolving to a name.
        users.Remove(user);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
