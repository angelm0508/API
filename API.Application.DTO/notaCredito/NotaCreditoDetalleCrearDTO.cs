namespace API.Application.DTO.notaCredito
{
    public class NotaCreditoDetalleCrearDTO
    {
        // Entry y NoLinea los asigna el servidor al registrar el documento.
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
    }
}
