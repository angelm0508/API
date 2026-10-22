namespace API.Application.DTO.ordenServicio
{
    public class OrdenServicioDetalleCrearDTO
    {
        // Solo lo usa POST api/OrdenServicioDetalle (la orden a la que se agrega); dentro de
        // OrdenServicioCrearDTO.Lineas lo asigna el servidor. Impuesto y TotalLinea no se reciben: los calcula el servidor.
        public int Entry { get; set; }
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
