using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Messaging.IntegrationTests;

// Fail only consumer connections; the real database stays available to verify
// that no business state was saved or acknowledged during the outage.
internal sealed class DatabaseOutage : DbConnectionInterceptor
{
    private int available;
    private int failures;
    private readonly TaskCompletionSource repeatedFailures = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task RepeatedFailures => repeatedFailures.Task;
    public void Recover() => Volatile.Write(ref available, 1);

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref available) != 0)
            return ValueTask.FromResult(result);
        // The old consumers abandoned a delivery after its third failed attempt.
        if (Interlocked.Increment(ref failures) >= 4)
            repeatedFailures.TrySetResult();
        throw new NpgsqlException("Simulated database outage.", new TimeoutException());
    }
}
