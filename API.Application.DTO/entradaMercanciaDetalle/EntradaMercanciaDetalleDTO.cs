namespace API.Application.DTO.entradaMercancia
{
    public class EntradaMercanciaDetalleDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? CostoUnitario { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? CodAlmacen { get; set; }
        public int? UomEntry { get; set; }
        public string? UomCodigo { get; set; }
        public decimal FactorUom { get; set; } = 1m;
        public decimal? CantidadBase { get; set; }
    }
}
