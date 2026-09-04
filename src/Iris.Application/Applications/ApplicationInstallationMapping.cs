using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;
using Iris.Domain.Infrastructure;
using Iris.Domain.Tenancy;

namespace Iris.Application.Applications;

internal static class ApplicationInstallationMapping
{
    public static ApplicationInstallationResponse ToResponse(
        this ApplicationInstallation installation,
        ApplicationDefinition application,
        ApplicationVersion version,
        ServerNode server,
        Customer customer,
        CustomerContext context) => new(
            installation.Id,
            installation.Name,
            application.Id,
            application.Name,
            application.Slug,
            version.Id,
            version.Version,
            installation.ApplicationUnitKey,
            installation.InstallationProfileKey,
            server.Id,
            server.Name,
            customer.Id,
            customer.Name,
            context.Id,
            context.Name,
            context.Kind.ToString(),
            installation.Notes,
            installation.IsActive,
            installation.Bindings.Select(binding => new ApplicationInstallationBindingResponse(
                binding.Id,
                binding.PlaceholderKey,
                binding.TargetKind,
                binding.TargetId,
                binding.TargetSlug,
                binding.ValuePreview,
                binding.Notes)).ToArray(),
            installation.CreatedAtUtc,
            installation.UpdatedAtUtc);

    /// <summary>
    /// Finds the <see cref="Customer"/> that owns a <see cref="CustomerContext"/> id — the FK
    /// every <see cref="ApplicationInstallation"/> carries. <see cref="ICustomerRepository"/> has
    /// no direct by-context lookup (contexts are owned entities, not their own aggregate), so this
    /// loads the accessible customers and correlates in memory, same as
    /// <c>ListApplicationInstallationsHandler</c> already does for application/server.
    /// </summary>
    public static async Task<(Customer Customer, CustomerContext Context)> ResolveCustomerContextAsync(
        this ICustomerRepository customers,
        Guid customerContextId,
        CancellationToken cancellationToken = default)
    {
        var all = await customers.GetAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (var customer in all)
        {
            var context = customer.Contexts.SingleOrDefault(c => c.Id == customerContextId);
            if (context is not null)
            {
                return (customer, context);
            }
        }

        throw new NotFoundException("Customer context", customerContextId);
    }
}
