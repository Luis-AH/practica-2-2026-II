using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models;

public class Notificacion
{
    public int Id { get; set; }

    [Required]
    public string MessageId { get; set; } = null!;

    [Required]
    public int SolicitudId { get; set; }

    [Required]
    public string UsuarioId { get; set; } = null!;

    [Required]
    public string Texto { get; set; } = null!;

    public DateTime FechaProcesamientoUtc { get; set; } = DateTime.UtcNow;
}
