namespace API.Application.DTO.notaDebitoCompra
{
    public class NotaDebitoCompraDetalleDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? CodAlmacen { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
    }
}
