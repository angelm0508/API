using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.pagoEfectuado
{
    /// <summary>Alta de un pago efectuado. El total lo calcula el servidor; no se acepta del proveedor.</summary>
    public class PagoEfectuadoCrearDTO
    {
        public int Serie { get; set; }
        /// <summary>Solo para series manuales.</summary>
        public int NumDoc { get; set; }
        public DateTime? FechaDoc { get; set; }
        public string? CodigoSn { get; set; }
        [StringLength(254, ErrorMessage = "El comentario no puede superar 254 caracteres.")]
        public string? Comentario { get; set; }
        public List<PagoEfectuadoFacturaCrearDTO> Facturas { get; set; } = new();
        public List<PagoEfectuadoMedioCrearDTO> Medios { get; set; } = new();
    }
}
