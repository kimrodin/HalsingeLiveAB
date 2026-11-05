using RabbitMQ.Client;

namespace Booking.Features.Tickets;

public sealed class RabbitMqConnectionProvider(IConfiguration configuration) : IAsyncDisposable
{
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private IConnection connection;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (connection is { IsOpen: true })
            return connection;

        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (connection is { IsOpen: true })
                return connection;

            if (connection is not null)
                await connection.DisposeAsync();

            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMq:HostName"] ?? "localhost",
                Port = configuration.GetValue("RabbitMq:Port", 5672),
                VirtualHost = configuration["RabbitMq:VirtualHost"] ?? "/",
                UserName = configuration["RabbitMq:UserName"] ?? "guest",
                Password = configuration["RabbitMq:Password"] ?? "guest",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };

            connection = await factory.CreateConnectionAsync(cancellationToken);
            return connection;
        }
        finally
        {
            connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
            await connection.DisposeAsync();
        connectionLock.Dispose();
    }
}
