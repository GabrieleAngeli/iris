using Iris.Application.Abstractions;
using Iris.Domain.Access;
using Iris.Domain.Applications;
using Iris.Domain.Audit;
using Iris.Domain.Infrastructure;
using Iris.Domain.Settings;
using Iris.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Iris.Infrastructure.Persistence.Interceptors;

public sealed class TransactionLogInterceptor(
    IClock clock,
    ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AddTransactionLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddTransactionLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddTransactionLogs(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not TransactionLogEntry)
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Metadata.FindPrimaryKey() is not null)
            .Select(ToPendingLog)
            .Where(e => e is not null)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var transactionId = Guid.CreateVersion7();
        var now = clock.UtcNow;
        var actorEmail = ValueOrSystem(currentUser.Email);
        var actorName = ValueOrSystem(currentUser.DisplayName);

        foreach (var pending in entries)
        {
            context.Add(TransactionLogEntry.Record(
                Guid.CreateVersion7(),
                transactionId,
                now,
                pending!.Area,
                pending.Action,
                pending.EntityType,
                pending.EntityId,
                currentUser.UserId,
                actorEmail,
                actorName,
                currentUser.ExternalId,
                pending.Summary));
        }
    }

    private static PendingTransactionLog? ToPendingLog(EntityEntry entry)
    {
        var clrType = entry.Metadata.ClrType;
        var area = AreaFor(clrType);
        if (area is null)
        {
            return null;
        }

        var entityType = clrType.Name;
        var action = entry.State switch
        {
            EntityState.Added => "Create",
            EntityState.Modified => "Update",
            EntityState.Deleted => "Delete",
            _ => "Change",
        };

        var id = EntityId(entry);
        return new PendingTransactionLog(area, action, entityType, id, $"{action} {entityType} {id}");
    }

    private static string? AreaFor(Type type)
    {
        if (type == typeof(UserSession))
        {
            return null;
        }

        if (type == typeof(User)
            || type == typeof(Role)
            || type == typeof(RoleAssignment)
            || type == typeof(UserInvitation)
            || type == typeof(EditLock))
        {
            return "Governance";
        }

        if (type == typeof(Customer) || type == typeof(CustomerContext))
        {
            return "Governance";
        }

        if (type == typeof(ServerNode) || type == typeof(ServerCredential) || type == typeof(DataServiceInstance))
        {
            return "Infrastructure";
        }

        if (type == typeof(ApplicationDefinition)
            || type == typeof(ApplicationVersion)
            || type == typeof(ConfigurationKey)
            || type == typeof(DependencyDefinition)
            || type == typeof(PlaceholderDefinition))
        {
            return "Applications";
        }

        if (type == typeof(MailProviderSettings))
        {
            return "Settings";
        }

        return null;
    }

    private static string EntityId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null || key.Properties.Count == 0)
        {
            return "-";
        }

        return string.Join(
            "|",
            key.Properties.Select(property =>
            {
                var value = entry.State == EntityState.Deleted
                    ? entry.Property(property.Name).OriginalValue
                    : entry.Property(property.Name).CurrentValue;
                return value?.ToString() ?? "-";
            }));
    }

    private static string ValueOrSystem(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "System" : value.Trim();

    private sealed record PendingTransactionLog(
        string Area,
        string Action,
        string EntityType,
        string EntityId,
        string Summary);
}
