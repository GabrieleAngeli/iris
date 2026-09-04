using Iris.Domain.Applications;

namespace Iris.Domain.Tests.Applications;

public sealed class InstallationRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    private static InstallationRun NewRun() =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), InstallationRunKind.AwxJob, """{"iris_x":"1"}""");

    [Fact]
    public void New_run_starts_pending_and_not_terminal()
    {
        var run = NewRun();

        Assert.Equal(InstallationRunStatus.Pending, run.Status);
        Assert.False(run.IsTerminal);
        Assert.Null(run.CompletedAtUtc);
        Assert.Null(run.ExternalJobId);
    }

    [Fact]
    public void MarkSubmitted_captures_the_job_handle_and_status()
    {
        var run = NewRun();

        run.MarkSubmitted("4242", "https://awx.example/#/jobs/4242", InstallationRunStatus.Running, "queued", Now);

        Assert.Equal("4242", run.ExternalJobId);
        Assert.Equal("https://awx.example/#/jobs/4242", run.ExternalUrl);
        Assert.Equal(InstallationRunStatus.Running, run.Status);
        Assert.Equal("queued", run.Message);
        Assert.Null(run.CompletedAtUtc);
    }

    [Fact]
    public void Reaching_a_terminal_status_stamps_completion_once()
    {
        var run = NewRun();

        run.MarkSubmitted("7", null, InstallationRunStatus.Running, null, Now);
        run.UpdateStatus(InstallationRunStatus.Succeeded, "done", Now);

        Assert.Equal(InstallationRunStatus.Succeeded, run.Status);
        Assert.True(run.IsTerminal);
        Assert.Equal(Now, run.CompletedAtUtc);

        // A later poll cannot move a terminal run.
        run.UpdateStatus(InstallationRunStatus.Running, "flapping", Now.AddMinutes(5));
        Assert.Equal(InstallationRunStatus.Succeeded, run.Status);
        Assert.Equal(Now, run.CompletedAtUtc);
    }

    [Fact]
    public void MarkFailed_is_ignored_after_success()
    {
        var run = NewRun();
        run.UpdateStatus(InstallationRunStatus.Succeeded, null, Now);

        run.MarkFailed("too late", Now.AddMinutes(1));

        Assert.Equal(InstallationRunStatus.Succeeded, run.Status);
    }

    [Fact]
    public void MarkFailed_requires_a_message()
    {
        var run = NewRun();

        Assert.Throws<ArgumentException>(() => run.MarkFailed(" ", Now));
    }
}
