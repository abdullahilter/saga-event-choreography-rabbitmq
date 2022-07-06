﻿using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace api.notify;

public class OrderCompletedSuccessfullyMessageConsumer : BackgroundService
{
    private readonly IModel _channel;
    private readonly IConnection _connection;
    private readonly INotifyService _notifyService;

    public OrderCompletedSuccessfullyMessageConsumer(INotifyService notifyService)
    {
        _notifyService = notifyService;

        var factory = new ConnectionFactory
        {
            Uri = new Uri("amqp://guest:guest@localhost:5672")
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(queue: "order.completedsuccessfully", durable: false, exclusive: false, autoDelete: false, arguments: null);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (model, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            var result = JsonSerializer.Deserialize<Order>(message);

            await _notifyService.OrderCompletedSuccessfullyAsync(result);

            _channel.BasicAck(eventArgs.DeliveryTag, false);
        };

        _channel.BasicConsume("order.completedsuccessfully", false, consumer);
    }

    public override void Dispose()
    {
        _channel.Close();
        _connection.Close();

        base.Dispose();
    }
}