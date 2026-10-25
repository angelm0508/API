namespace API.Application.DTO.pagoEfectuado
{
    /// <summary>Factura de compra o nota de débito del proveedor con su saldo, para el formulario de nuevo pago.</summary>
    public class DocumentoPorPagarDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        /// <summary>"--" = factura de compra; "DN" = nota de débito de proveedor.</summary>
        public string? DocSubType { get; set; }
        public DateTime? FechaDoc { get; set; }
        public decimal Total { get; set; }
        public decimal Acreditado { get; set; }
        public decimal Pagado { get; set; }
        public decimal Saldo { get; set; }
        public string Estado { get; set; } = null!;
    }
}
