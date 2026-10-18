using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.notaDebitoCompra
{
    public class NotaDebitoCompraCrearDTO
    {
        // Requerido solo cuando la serie elegida es "Manual": para series autogeneradas el
        // servidor calcula el siguiente numero (ver NotaDebitoCompraDomain.InsertarAsync).
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? TipoObjeto { get; set; }
        /// <summary>Tipo de nota: "I" = por artículos (mueve inventario), "S" = por concepto. Obligatorio al crear.</summary>
        public string? DocType { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }

        /// <summary>Lineas del documento (artículos o conceptos según DocType): se registran junto con el encabezado y no se pueden modificar despues.</summary>
        public List<NotaDebitoCompraDetalleCrearDTO> Lineas { get; set; } = new();
    }
}
