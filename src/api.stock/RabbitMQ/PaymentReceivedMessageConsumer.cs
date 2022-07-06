﻿using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace api.stock;

public class PaymentReceivedMessageConsumer : BackgroundService
{
    private readonly IModel _channel;
    private readonly IConnection _connection;
    private readonly IStockService _stockService;

    public PaymentReceivedMessageConsumer(IStockService stockService)
    {
        _stockService = stockService;

        var factory = new ConnectionFactory
        {
            Uri = new Uri("amqp://guest:guest@localhost:5672")
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(queue: "payment.received", durable: false, exclusive: false, autoDelete: false, arguments: null);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (model, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            var result = JsonSerializer.Deserialize<Payment>(message);

            await _stockService.ReduceAsync(result);

            _channel.BasicAck(eventArgs.DeliveryTag, false);
        };

        _channel.BasicConsume("payment.received", false, consumer);
    }

    public override void Dispose()
    {
        _channel.Close();
        _connection.Close();

        base.Dispose();
    }
}