using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace PlataformaCreditos.Services;

public class MensajeriaService : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly string _queueName;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly ILogger<MensajeriaService> _logger;

    public MensajeriaService(IConfiguration configuration, ILogger<MensajeriaService> logger)
    {
        _connectionString = configuration["RabbitMq:ConnectionString"]!;
        _queueName = configuration["RabbitMq:QueueName"]!;
        _logger = logger;
    }

    private async Task EnsureConnectedAsync()
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true) return;

        var factory = new ConnectionFactory
        {
            Uri = new Uri(_connectionString)
        };

        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }

    /// <summary>
    /// Publica un mensaje en la cola de RabbitMQ cuando se registra una nueva solicitud.
    /// </summary>
    public async Task PublicarSolicitudCreadaAsync(int solicitudId, string userId, decimal monto)
    {
        try
        {
            await EnsureConnectedAsync();

            var mensaje = new
            {
                MessageId = Guid.NewGuid().ToString(),
                SolicitudId = solicitudId,
                UsuarioId = userId,
                Monto = monto,
                Timestamp = DateTime.UtcNow
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(mensaje));

            var props = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = mensaje.MessageId
            };

            await _channel!.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _queueName,
                mandatory: false,
                basicProperties: props,
                body: body);

            _logger.LogInformation("Mensaje publicado en RabbitMQ para solicitud #{SolicitudId}", solicitudId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al publicar mensaje en RabbitMQ");
            // No propagamos el error para no interrumpir el flujo principal de la app
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel?.IsOpen == true)
            await _channel.CloseAsync();
        if (_connection?.IsOpen == true)
            await _connection.CloseAsync();
    }
}
