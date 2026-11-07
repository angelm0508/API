namespace API.Application.DTO.articulo.almacen
{
    /// <summary>Configuración de un almacén para un artículo. El PUT reemplaza toda la configuración del artículo.</summary>
    public class ArticuloAlmacenGuardarDTO
    {
        public string CodAlmacen { get; set; } = null!;
        public decimal? Minimo { get; set; }
        public decimal? Maximo { get; set; }
        public bool Bloqueado { get; set; }
    }
}
