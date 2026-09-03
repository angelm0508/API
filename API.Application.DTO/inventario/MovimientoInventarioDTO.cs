namespace API.Application.DTO.inventario
{
    public class MovimientoInventarioDTO
    {
        public int Entry { get; set; }
        public string TipoDoc { get; set; } = null!;
        public int DocEntry { get; set; }
        public int DocLinea { get; set; }
        public string CodArticulo { get; set; } = null!;
        public string CodAlmacen { get; set; } = null!;
        public DateTime Fecha { get; set; }
        public decimal CantidadEntra { get; set; }
        public decimal CantidadSale { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal CostoUnitario { get; set; }
        public decimal ValorMovimiento { get; set; }
        public decimal VariacionPrecio { get; set; }
        public decimal SaldoCantidad { get; set; }
        public decimal SaldoCostoPromedio { get; set; }
        public decimal SaldoValor { get; set; }
        public int? MovReversaDe { get; set; }

        /// <summary>Nombre del tipo de documento (DocAlias de NumeracionDocumento); si no hay, el propio código.</summary>
        public string? TipoDocNombre { get; set; }
    }
}
