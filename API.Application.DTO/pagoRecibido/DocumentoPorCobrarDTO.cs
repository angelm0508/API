namespace API.Application.DTO.pagoRecibido
{
    /// <summary>Factura o nota de débito del cliente con su saldo, para el formulario de nuevo pago.</summary>
    public class DocumentoPorCobrarDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        /// <summary>"--" = factura; "ND" = nota de débito de cliente.</summary>
        public string? DocSubType { get; set; }
        public DateTime? FechaDoc { get; set; }
        public decimal Total { get; set; }
        public decimal Acreditado { get; set; }
        public decimal Pagado { get; set; }
        public decimal Saldo { get; set; }
        public string Estado { get; set; } = null!;
    }
}
