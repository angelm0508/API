using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.factura
{
    public class FacturaCrearDTO
    {
        // Requerido solo cuando la serie elegida es "Manual" -- para series autogeneradas el
        // servidor calcula el siguiente número al momento de registrar el factura (ver
        // FacturaDomain.InsertarAsync), así que aquí no puede ser obligatorio.
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
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

        /// <summary>Condición de pago (código). Opcional: sin ella se usa la del socio o CON. El vencimiento lo calcula el servidor.</summary>
        public string? CondicionPago { get; set; }

        /// <summary>
        /// Líneas del documento. Con INV-3 el documento se registra con sus líneas en una
        /// sola petición (y una sola transacción). El `Entry` de cada línea se ignora aquí
        /// (lo asigna el servidor al crear el encabezado).
        /// </summary>
        public List<FacturaDetalleCrearDTO> Lineas { get; set; } = new();

        /// <summary>El usuario pide crear la factura aunque supere el límite de crédito del cliente (requiere el permiso AutorizarExcesoCredito).</summary>
        public bool AutorizarExcesoCredito { get; set; }

        /// <summary>
        /// Monto que el usuario va a cobrar en el acto (se registra aparte en PagosRecibidos). Se resta del monto que el
        /// control de crédito compara con el límite; no puede exceder el total del documento. Nulo equivale a 0.
        /// </summary>
        public decimal? PagoInmediato { get; set; }
    }
}
