using Iris.Domain.Deployments;

namespace Iris.Domain.Tests.Deployments;

public sealed class EnvironmentServerAssignmentTests
{
    [Fact]
    public void Constructor_sets_fields_and_trims_notes()
    {
        var id = Guid.CreateVersion7();
        var contextId = Guid.CreateVersion7();
        var serverId = Guid.CreateVersion7();

        var assignment = new EnvironmentServerAssignment(id, contextId, serverId, "  primary engine host  ");

        Assert.Equal(id, assignment.Id);
        Assert.Equal(contextId, assignment.CustomerContextId);
        Assert.Equal(serverId, assignment.ServerNodeId);
        Assert.Equal("primary engine host", assignment.Notes);
    }

    [Fact]
    public void Constructor_normalizes_blank_notes_to_null()
    {
        var assignment = new EnvironmentServerAssignment(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "   ");

        Assert.Null(assignment.Notes);
    }
}
