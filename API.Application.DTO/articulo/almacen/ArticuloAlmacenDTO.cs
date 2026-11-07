namespace API.Application.DTO.articulo.almacen
{
    /// <summary>Fila de la ficha de stock por almacén de un artículo.</summary>
    public class ArticuloAlmacenDTO
    {
        public string CodAlmacen { get; set; } = null!;
        public string NombreAlmacen { get; set; } = null!;
        public bool Bloqueado { get; set; }
        public decimal EnStock { get; set; }
        public decimal Comprometido { get; set; }
        public decimal Pedido { get; set; }
        public decimal Disponible { get; set; }
        public decimal? Minimo { get; set; }
        public decimal? Maximo { get; set; }
        public decimal? NivelNecesario { get; set; }
        /// <summary>OK, BAJO_MINIMO o SOBRE_MAXIMO.</summary>
        public string Estado { get; set; } = null!;
    }
}
