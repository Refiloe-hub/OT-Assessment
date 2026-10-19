using System.Text.Json;
using Microsoft.Extensions.Options;
using OT.Assessment.Consumer.Messaging;
using OT.Assessment.Consumer.Models;
using OT.Assessment.Consumer.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OT.Assessment.Consumer.Workers;

public class WagerConsumerService : BackgroundService
{
    private readonly RabbitMqOptions _options;
    private readonly IWagerRepository _repository;
    private readonly ILogger<WagerConsumerService> _logger;

    public WagerConsumerService(
        IOptions<RabbitMqOptions> options,
        IWagerRepository repository,
        ILogger<WagerConsumerService> logger)
    {
        _options = options.Value;
        _repository = repository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(_options.QueueName, durable: true, exclusive: false,
            autoDelete: false, arguments: null, cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, _options.PrefetchCount, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            CasinoWagerMessage wager;
            try
            {
                wager = JsonSerializer.Deserialize<CasinoWagerMessage>(args.Body.Span);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Discarding malformed message");
                await channel.BasicNackAsync(args.DeliveryTag, false, false, stoppingToken);
                return;
            }

            try
            {
                await _repository.SaveAsync(wager, stoppingToken);
                await channel.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to save wager {WagerId}, requeueing", wager?.WagerId);
                await channel.BasicNackAsync(args.DeliveryTag, false, true, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(_options.QueueName, autoAck: false, consumer, stoppingToken);
        _logger.LogInformation("Consuming from queue {Queue}", _options.QueueName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }
}