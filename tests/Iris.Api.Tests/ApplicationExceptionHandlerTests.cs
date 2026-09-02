using Iris.Api;
using Iris.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Iris.Api.Tests;

public sealed class ApplicationExceptionHandlerTests
{
    [Fact]
    public async Task Mapped_exception_is_handled_and_logged_as_a_warning()
    {
        var logger = new RecordingLogger<ApplicationExceptionHandler>();
        var handler = new ApplicationExceptionHandler(new NoopProblemDetailsService(), logger);
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(context, new NotFoundException("Customer", Guid.NewGuid()), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Fact]
    public async Task Unmapped_exception_is_not_handled_but_logged_as_an_error()
    {
        var logger = new RecordingLogger<ApplicationExceptionHandler>();
        var handler = new ApplicationExceptionHandler(new NoopProblemDetailsService(), logger);
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        Assert.False(handled);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    /// <summary>
    /// <see cref="IProblemDetailsService.TryWriteAsync"/> is a default interface method whose
    /// built-in body only succeeds for the framework's own <c>ProblemDetailsService</c> — a
    /// custom implementation that only provides <see cref="WriteAsync"/> always gets the
    /// default body's <c>false</c>. Implementing both members directly here sidesteps that.
    /// </summary>
    private sealed class NoopProblemDetailsService : IProblemDetailsService
    {
        public ValueTask WriteAsync(ProblemDetailsContext context) => ValueTask.CompletedTask;

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context) => ValueTask.FromResult(true);
    }

    /// <summary>Minimal <see cref="ILogger{T}"/> test double recording level + exception per call.</summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception));
    }
}
