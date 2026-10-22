namespace API.Application.DTO.ordenServicio
{
    public class OrdenServicioDetalleActualizarDTO
    {
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public string? CodAlmacen { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public string? CodMecanico { get; set; }
    }
}
