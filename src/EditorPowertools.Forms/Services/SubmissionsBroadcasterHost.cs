using Microsoft.Extensions.Hosting;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Services;

/// <summary>
/// Wraps <see cref="SubmissionsBroadcaster"/> as an <see cref="IHostedService"/>
/// purely so the runtime disposes it on app shutdown — that triggers the event
/// unhook logic and avoids leaking handler references on the static
/// <c>FormsEvents.Instance</c>.
/// </summary>
internal sealed class SubmissionsBroadcasterHost : IHostedService
{
    private readonly SubmissionsBroadcaster _broadcaster;

    public SubmissionsBroadcasterHost(SubmissionsBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _broadcaster.Dispose();
        return Task.CompletedTask;
    }
}
