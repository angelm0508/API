using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.formaPago
{
    public class FormaPagoActualizarDTO
    {
        [StringLength(50, ErrorMessage = "El nombre no puede superar 50 caracteres.")]
        public string Nombre { get; set; } = null!;
        public string? TipoDato { get; set; }
        public string? Activo { get; set; }
    }
}
