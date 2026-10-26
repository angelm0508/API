using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.condicionPago
{
    public class CondicionPagoCrearDTO
    {
        [StringLength(10, ErrorMessage = "El código no puede superar 10 caracteres.")]
        public string Codigo { get; set; } = null!;
        [StringLength(50, ErrorMessage = "El nombre no puede superar 50 caracteres.")]
        public string Nombre { get; set; } = null!;
        [Required(ErrorMessage = "Los días de crédito son requeridos.")]
        [Range(0, 365, ErrorMessage = "Los días de crédito deben estar entre 0 y 365.")]
        public int? Dias { get; set; }
        public string? Activo { get; set; }
    }
}
