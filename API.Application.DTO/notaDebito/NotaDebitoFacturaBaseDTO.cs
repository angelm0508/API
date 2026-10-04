namespace API.Application.DTO.notaDebito
{
    /// <summary>Factura base con sus lineas convertidas en conceptos listos para precargar la nota.</summary>
    public class NotaDebitoFacturaBaseDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? MonedaDoc { get; set; }
        public string? Direccion { get; set; }
        public List<NotaDebitoDetalleCrearDTO> Lineas { get; set; } = new();
    }
}
