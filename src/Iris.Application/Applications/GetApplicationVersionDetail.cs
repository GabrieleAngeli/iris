using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;

namespace Iris.Application.Applications;

/// <summary>Query for <c>GET /applications/{applicationId}/versions/{versionId}</c>.</summary>
public sealed record GetApplicationVersionDetailQuery(Guid ApplicationId, Guid VersionId);

public sealed class GetApplicationVersionDetailHandler(IApplicationRepository applications)
{
    public async Task<ApplicationVersionDetailResponse> HandleAsync(
        GetApplicationVersionDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var application = await applications.GetAsync(query.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Application", query.ApplicationId);

        var version = application.Versions.SingleOrDefault(v => v.Id == query.VersionId)
            ?? throw new NotFoundException("Application version", query.VersionId);

        return version.ToDetailResponse();
    }
}
