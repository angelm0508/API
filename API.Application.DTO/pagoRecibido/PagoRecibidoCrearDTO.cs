using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.pagoRecibido
{
    /// <summary>Alta de un pago recibido. El total lo calcula el servidor; no se acepta del cliente.</summary>
    public class PagoRecibidoCrearDTO
    {
        public int Serie { get; set; }
        /// <summary>Solo para series manuales.</summary>
        public int NumDoc { get; set; }
        public DateTime? FechaDoc { get; set; }
        public string? CodigoSn { get; set; }
        [StringLength(254, ErrorMessage = "El comentario no puede superar 254 caracteres.")]
        public string? Comentario { get; set; }
        public List<PagoRecibidoFacturaCrearDTO> Facturas { get; set; } = new();
        public List<PagoRecibidoMedioCrearDTO> Medios { get; set; } = new();
    }
}
