using PlataformaCreditos.Models;

namespace PlataformaCreditos.Models.ViewModels;

public class SolicitudFiltroViewModel
{
    // Filtros
    public EstadoSolicitud? Estado { get; set; }
    public decimal? MontoMinimo { get; set; }
    public decimal? MontoMaximo { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }

    // Resultados
    public List<SolicitudCredito> Solicitudes { get; set; } = new();

    // Validaciones
    public string? ErrorFiltro { get; set; }
}
