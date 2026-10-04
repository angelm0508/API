namespace API.Application.DTO.notaDebitoCompra
{
    /// <summary>Factura de compra base con sus lineas convertidas en conceptos listos para precargar la nota.</summary>
    public class NotaDebitoCompraFacturaBaseDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? MonedaDoc { get; set; }
        public string? Direccion { get; set; }
        public List<NotaDebitoCompraDetalleCrearDTO> Lineas { get; set; } = new();
    }
}
