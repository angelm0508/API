namespace API.Application.DTO.entradaMercancia
{
    public class EntradaMercanciaDetalleActualizarDTO
    {
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? CostoUnitario { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
