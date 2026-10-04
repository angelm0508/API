namespace API.Application.DTO.notaCreditoCompra
{
    /// <summary>Factura de compra base con sus lineas convertidas en conceptos y su saldo acreditable.</summary>
    public class NotaCreditoCompraFacturaBaseDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? MonedaDoc { get; set; }
        public string? Direccion { get; set; }
        public decimal? TotalDoc { get; set; }
        public decimal TotalAcreditado { get; set; }
        public decimal Disponible { get; set; }
        public List<NotaCreditoCompraDetalleCrearDTO> Lineas { get; set; } = new();
    }
}
