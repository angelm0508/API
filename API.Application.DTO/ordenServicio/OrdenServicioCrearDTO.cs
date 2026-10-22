using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.ordenServicio
{
    public class OrdenServicioCrearDTO
    {
        // Requerido solo cuando la serie elegida es "Manual": para series autogeneradas el servidor
        // calcula el siguiente número (ver OrdenServicioDomain.InsertarAsync).
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public DateTime? FechaPromesa { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int MotoEntry { get; set; }
        public string? CodMecanico { get; set; }
        public int? KmIngreso { get; set; }
        public string? FallaReportada { get; set; }
        public string? Diagnostico { get; set; }
        public string? Observaciones { get; set; }
        public string? Comentario { get; set; }

        /// <summary>Líneas (insumos y servicios) que se registran junto con la orden. Los montos los calcula el servidor.</summary>
        public List<OrdenServicioDetalleCrearDTO> Lineas { get; set; } = new();
    }
}
