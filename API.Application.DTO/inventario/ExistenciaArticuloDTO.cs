namespace API.Application.DTO.inventario
{
    public class ExistenciaArticuloDTO
    {
        public string CodArticulo { get; set; } = null!;
        public string CodAlmacen { get; set; } = null!;
        public decimal Disponible { get; set; }
        public decimal Comprometido { get; set; }
        public decimal Pedido { get; set; }
        public DateTime FechaActualizacion { get; set; }
        /// <summary>Configuración de stock por almacén; la llena la capa de aplicación (nulo = sin nivel).</summary>
        public decimal? Minimo { get; set; }
        public decimal? Maximo { get; set; }
        public bool Bloqueado { get; set; }
        /// <summary>OK, BAJO_MINIMO o SOBRE_MAXIMO.</summary>
        public string Estado { get; set; } = "OK";
    }
}
