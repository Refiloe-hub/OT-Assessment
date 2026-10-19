using System.Text.Json;
using Microsoft.Extensions.Options;
using OT.Assessment.App.Models;
using RabbitMQ.Client;

namespace OT.Assessment.App.Messaging;

public sealed class RabbitMqWagerPublisher : IWagerPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection _connection;
    private IChannel _channel;

    public RabbitMqWagerPublisher(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task PublishAsync(CasinoWagerRequest wager, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(wager);

        // A RabbitMQ channel is not thread-safe, so publishes are serialised.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureChannelAsync(cancellationToken);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _options.QueueName,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true }) return;

        if (_connection is not { IsOpen: true })
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password
            };
            _connection = await factory.CreateConnectionAsync(cancellationToken);
        }

        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await _channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
        _gate.Dispose();
    }
}