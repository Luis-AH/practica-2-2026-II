using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace PlataformaCreditos.Services;

/// <summary>
/// Consumidor de fondo que escucha la cola de RabbitMQ y persiste notificaciones en SQLite.
/// Usa ACK manual: solo confirma el mensaje si fue guardado exitosamente (At-Least-Once delivery).
/// </summary>
public class NotificacionesConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificacionesConsumerService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public NotificacionesConsumerService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<NotificacionesConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>("RabbitMq:ConsumerEnabled"))
        {
            _logger.LogInformation("Consumidor RabbitMQ deshabilitado por configuracion.");
            return;
        }

        var connectionString = _configuration["RabbitMq:ConnectionString"]!;
        var queueName = _configuration["RabbitMq:QueueName"]!;

        try
        {
            var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await _channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            // Prefetch de 1: procesar un mensaje a la vez (garantiza At-Least-Once)
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

            _logger.LogInformation("Consumidor RabbitMQ iniciado, escuchando la cola '{Queue}'", queueName);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                _logger.LogInformation("Mensaje recibido de RabbitMQ: {Body}", body);

                var procesadoOk = false;

                try
                {
                    var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    var messageId = root.GetProperty("MessageId").GetString()!;
                    var solicitudId = root.GetProperty("SolicitudId").GetInt32();
                    var usuarioId = root.GetProperty("UsuarioId").GetString()!;
                    var monto = root.GetProperty("Monto").GetDecimal();

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // Idempotencia: solo guardar si el MessageId no existe
                    var existe = await db.Notificaciones.AnyAsync(n => n.MessageId == messageId);
                    if (!existe)
                    {
                        db.Notificaciones.Add(new Notificacion
                        {
                            MessageId = messageId,
                            SolicitudId = solicitudId,
                            UsuarioId = usuarioId,
                            Texto = $"Se ha registrado correctamente tu solicitud #{solicitudId} por S/ {monto:N2}. Será evaluada pronto.",
                            FechaProcesamientoUtc = DateTime.UtcNow
                        });

                        await db.SaveChangesAsync();
                        _logger.LogInformation("Notificacion #{SolicitudId} guardada en BD", solicitudId);
                    }
                    else
                    {
                        _logger.LogWarning("Mensaje duplicado ignorado: {MessageId}", messageId);
                    }

                    procesadoOk = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando mensaje RabbitMQ");
                }
                finally
                {
                    if (procesadoOk)
                        // ACK manual: confirmamos que el mensaje fue procesado
                        await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    else
                        // NACK: devolver a la cola (no requeue para evitar loops infinitos)
                        await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

            // Mantener el servicio vivo hasta que se cancele
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Consumidor RabbitMQ detenido.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico en el consumidor RabbitMQ");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel?.IsOpen == true) await _channel.CloseAsync();
        if (_connection?.IsOpen == true) await _connection.CloseAsync();
    }
}
