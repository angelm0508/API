using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.condicionPago
{
    public class CondicionPagoActualizarDTO
    {
        [StringLength(50, ErrorMessage = "El nombre no puede superar 50 caracteres.")]
        public string Nombre { get; set; } = null!;
        [Range(0, 365, ErrorMessage = "Los días de crédito deben estar entre 0 y 365.")]
        public int Dias { get; set; }
        public string? Activo { get; set; }
    }
}
