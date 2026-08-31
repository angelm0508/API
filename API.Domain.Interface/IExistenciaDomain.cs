using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IExistenciaDomain
    {
        Task<IEnumerable<ExistenciaArticulo>> ObtenerTodoAsync(string? articulo, string? almacen);
        Task<ExistenciaArticulo?> ObtenerAsync(string codArticulo, string codAlmacen);
        Task<IEnumerable<ExistenciaArticulo>> ObtenerPorArticuloAsync(string codArticulo);
    }
}
