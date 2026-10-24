using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.formaPago
{
    public class FormaPagoCrearDTO
    {
        [StringLength(10, ErrorMessage = "El código no puede superar 10 caracteres.")]
        public string Codigo { get; set; } = null!;
        [StringLength(50, ErrorMessage = "El nombre no puede superar 50 caracteres.")]
        public string Nombre { get; set; } = null!;
        public string? TipoDato { get; set; }
        public string? Activo { get; set; }
    }
}
