namespace API.Application.DTO.notaDebito
{
    public class NotaDebitoDetalleCrearDTO
    {
        // Entry y NoLinea los asigna el servidor al registrar el documento.
        // Solo en notas por artículos (DocType "I"); en notas por concepto deben ir vacíos.
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
    }
}
