using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models.ViewModels;

public class NuevaSolicitudViewModel
{
    [Required(ErrorMessage = "El monto es obligatorio.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
    public decimal MontoSolicitado { get; set; }
}
