namespace API.Domain.Interface
{
    /// <summary>Resultado de resolver el precio de un artículo contra una lista de precio.</summary>
    public record PrecioResuelto(string Codigo, decimal PrecioBase, decimal FactorEfectivo, decimal PrecioLista);

    public interface IPrecioListaDomain
    {
        /// <summary>
        /// Devuelve el precio del artículo aplicando la lista de precio indicada (cadena Base
        /// recursiva de factores + redondeo de la lista asignada). Si <paramref name="listaPrecioEntry"/>
        /// es null, devuelve el PrecioUnitario base redondeado a 2 decimales.
        /// Lanza <see cref="API.Domain.Core.Inventario.ArticuloNoExisteException"/> si el artículo no existe.
        /// </summary>
        Task<PrecioResuelto> ResolverAsync(string codigoArticulo, int? listaPrecioEntry);
    }
}
