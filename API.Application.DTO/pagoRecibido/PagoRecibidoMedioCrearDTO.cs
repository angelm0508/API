using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.pagoRecibido
{
    public class PagoRecibidoMedioCrearDTO
    {
        public string? CodFormaPago { get; set; }
        public decimal Monto { get; set; }
        [StringLength(50, ErrorMessage = "La referencia no puede superar 50 caracteres.")]
        public string? Referencia { get; set; }
        [StringLength(60, ErrorMessage = "El banco no puede superar 60 caracteres.")]
        public string? Banco { get; set; }
        public DateTime? FechaCheque { get; set; }
    }
}
