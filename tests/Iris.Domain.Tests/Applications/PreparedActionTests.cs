using Iris.Domain.Applications;

namespace Iris.Domain.Tests.Applications;

public sealed class PreparedActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static PreparedAction NewAction() =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), """{"plan":true}""", """{"isValid":true}""",
            42, "cloud_02_trial", null, false);

    [Fact]
    public void New_action_starts_prepared_and_not_terminal()
    {
        var action = NewAction();

        Assert.Equal(PreparedActionStatus.Prepared, action.Status);
        Assert.False(action.IsTerminal);
        Assert.Null(action.InstallationRunId);
        Assert.Null(action.ExecutedAtUtc);
        Assert.Null(action.CanceledAtUtc);
    }

    [Fact]
    public void Cancel_transitions_to_canceled_and_records_the_reason()
    {
        var action = NewAction();

        action.Cancel("operator changed their mind", Now);

        Assert.Equal(PreparedActionStatus.Canceled, action.Status);
        Assert.True(action.IsTerminal);
        Assert.Equal("operator changed their mind", action.CancelReason);
        Assert.Equal(Now, action.CanceledAtUtc);
    }

    [Fact]
    public void Cancel_is_a_no_op_once_already_terminal()
    {
        var action = NewAction();
        action.Cancel("first", Now);

        action.Cancel("second", Now.AddMinutes(5));

        Assert.Equal("first", action.CancelReason);
        Assert.Equal(Now, action.CanceledAtUtc);
    }

    [Fact]
    public void MarkExecuted_transitions_to_executed_and_records_the_run_id()
    {
        var action = NewAction();
        var runId = Guid.CreateVersion7();

        action.MarkExecuted(runId, Now);

        Assert.Equal(PreparedActionStatus.Executed, action.Status);
        Assert.True(action.IsTerminal);
        Assert.Equal(runId, action.InstallationRunId);
        Assert.Equal(Now, action.ExecutedAtUtc);
    }

    [Fact]
    public void MarkExecuted_throws_when_already_executed()
    {
        var action = NewAction();
        action.MarkExecuted(Guid.CreateVersion7(), Now);

        Assert.Throws<InvalidOperationException>(() => action.MarkExecuted(Guid.CreateVersion7(), Now.AddMinutes(1)));
    }

    [Fact]
    public void MarkExecuted_throws_when_already_canceled()
    {
        var action = NewAction();
        action.Cancel(null, Now);

        Assert.Throws<InvalidOperationException>(() => action.MarkExecuted(Guid.CreateVersion7(), Now.AddMinutes(1)));
    }
}
