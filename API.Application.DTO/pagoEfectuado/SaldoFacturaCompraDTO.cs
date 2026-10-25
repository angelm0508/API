namespace API.Application.DTO.pagoEfectuado
{
    /// <summary>Saldo y estado de pago de una factura de compra o nota de débito de proveedor.</summary>
    public class SaldoFacturaCompraDTO
    {
        public int FacturaCompraEntry { get; set; }
        public decimal Total { get; set; }
        public decimal Acreditado { get; set; }
        public decimal Pagado { get; set; }
        public decimal Saldo { get; set; }
        /// <summary>Pendiente, Parcial o Pagada.</summary>
        public string Estado { get; set; } = null!;
    }
}
