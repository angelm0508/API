namespace API.Application.DTO.notaDebitoCompra
{
    /// <summary>Resumen de una factura de compra candidata a base de una nota de debito.</summary>
    public class NotaDebitoCompraFacturaBaseResumenDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public DateTime? FechaDoc { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? MonedaDoc { get; set; }
    }
}
