namespace API.Domain.Interface
{
    public sealed record ArticuloAlmacenFila(
        string CodAlmacen, string NombreAlmacen, bool Bloqueado,
        decimal EnStock, decimal Comprometido, decimal Pedido, decimal Disponible,
        decimal? Minimo, decimal? Maximo, decimal? NivelNecesario, string Estado);

    public sealed record ArticuloAlmacenConfig(string CodAlmacen, decimal? Minimo, decimal? Maximo, bool Bloqueado);

    public sealed record AlmacenBajoMinimo(
        string CodArticulo, string NombreArticulo, string CodAlmacen, string NombreAlmacen,
        decimal EnStock, decimal Minimo);

    public interface IArticuloAlmacenDomain
    {
        /// <summary>Filas de la ficha. Lanza ArticuloNoExisteException si el artículo no existe.</summary>
        Task<IReadOnlyList<ArticuloAlmacenFila>> ObtenerFilasAsync(string codArticulo);
        /// <summary>Reemplaza TODA la configuración del artículo (atómico). Valida y lanza Exception con el mensaje del contrato.</summary>
        Task GuardarAsync(string codArticulo, IReadOnlyList<ArticuloAlmacenConfig> filas);
        /// <summary>Configuración existente por (artículo, almacén), filtrable; para enriquecer Existencias.</summary>
        Task<IReadOnlyDictionary<(string CodArticulo, string CodAlmacen), ArticuloAlmacenConfig>> ObtenerConfiguracionAsync(string? articulo, string? almacen);
        /// <summary>Artículos de inventario activos bajo su mínimo por almacén (mínimo &gt; 0, en stock &lt; mínimo).</summary>
        Task<IReadOnlyList<AlmacenBajoMinimo>> ObtenerBajoMinimoAsync();
    }
}
