using Iris.Application.Common;
using Iris.Domain.Infrastructure;
using Iris.Domain.Tenancy;

namespace Iris.Application.Infrastructure;

internal static class DataServiceParsing
{
    public static (DataServiceKind Kind, ContextKind Environment) Parse(
        string name,
        string kind,
        string endpoint,
        int? port,
        int? storageGb,
        string environment)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Data service name is required.");
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ValidationException("Data service endpoint is required.");
        }

        if (!Enum.TryParse<DataServiceKind>(kind, ignoreCase: true, out var parsedKind))
        {
            throw new ValidationException("Unknown data service kind. Expected Mssql, PostgreSql or Redis.");
        }

        if (port is < 1 or > 65535)
        {
            throw new ValidationException("Port must be between 1 and 65535.");
        }

        if (storageGb < 0)
        {
            throw new ValidationException("Storage GB cannot be negative.");
        }

        if (!Enum.TryParse<ContextKind>(environment, ignoreCase: true, out var parsedEnvironment))
        {
            throw new ValidationException("Unknown environment. Expected Test, Staging or Production.");
        }

        return (parsedKind, parsedEnvironment);
    }
}
