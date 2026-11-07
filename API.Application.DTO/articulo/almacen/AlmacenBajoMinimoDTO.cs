namespace API.Application.DTO.articulo.almacen
{
    /// <summary>Artículo cuyo stock en un almacén está por debajo de su mínimo.</summary>
    public class AlmacenBajoMinimoDTO
    {
        public string CodArticulo { get; set; } = null!;
        public string NombreArticulo { get; set; } = null!;
        public string CodAlmacen { get; set; } = null!;
        public string NombreAlmacen { get; set; } = null!;
        public decimal EnStock { get; set; }
        public decimal Minimo { get; set; }
    }
}
