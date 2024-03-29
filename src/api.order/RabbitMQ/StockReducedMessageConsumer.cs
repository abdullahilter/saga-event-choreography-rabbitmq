﻿using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace api.order;

public class StockReducedMessageConsumer : BackgroundService
{
    private readonly IModel _channel;
    private readonly IConnection _connection;
    private readonly IOrderService _orderService;
    private readonly IOptions<RabbitMQOptions> _options;

    public StockReducedMessageConsumer(IOrderService orderService, IOptions<RabbitMQOptions> options)
    {
        _orderService = orderService;
        _options = options;

        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.Value.Uri)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(queue: "stock.reduced", durable: false, exclusive: false, autoDelete: false, arguments: null);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (model, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            var result = JsonSerializer.Deserialize<Stock>(message);

            await _orderService.SetStatusToSuccessfullyAsync(result);

            _channel.BasicAck(eventArgs.DeliveryTag, false);
        };

        _channel.BasicConsume("stock.reduced", false, consumer);
    }

    public override void Dispose()
    {
        _channel.Close();
        _connection.Close();

        base.Dispose();
    }
}