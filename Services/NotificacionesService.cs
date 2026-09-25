using PusherServer;

namespace PlataformaCreditos.Services;

public class NotificacionesService
{
    private readonly Pusher _pusher;

    public NotificacionesService(IConfiguration configuration)
    {
        var options = new PusherOptions
        {
            Cluster = configuration["PieSocket:Cluster"],
            HostName = $"{configuration["PieSocket:Cluster"]}.piesocket.com",
            Encrypted = true
        };

        _pusher = new Pusher(
            configuration["PieSocket:AppId"],
            configuration["PieSocket:ApiKey"],
            configuration["PieSocket:ApiSecret"],
            options);
    }

    public async Task EnviarEstadoActualizadoAsync(string userId, int solicitudId, string estado, string motivo)
    {
        var channel = $"private-solicitudes-{userId}";
        var eventName = "SolicitudEstadoActualizado";
        var payload = new 
        { 
            SolicitudId = solicitudId, 
            Estado = estado,
            MotivoRechazo = motivo 
        };

        await _pusher.TriggerAsync(channel, eventName, payload);
    }

    public object AutenticarConexion(string channelName, string socketId)
    {
        return _pusher.Authenticate(channelName, socketId);
    }
}
