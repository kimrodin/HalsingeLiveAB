namespace Booking.IntegrationTests;

// All participants must reach the contested operation before any can continue.
internal sealed class ConcurrentRequestGate(int participants)
{
    private int remaining = participants;
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task ArriveAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Decrement(ref remaining) == 0)
            release.TrySetResult();

        return release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }
}
