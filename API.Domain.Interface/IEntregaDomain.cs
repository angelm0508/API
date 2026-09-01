using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IEntregaDomain
    {
        #region async methods
        Task<int> InsertarAsync(Entrega obj, IEnumerable<EntregaDetalle> lineas);
        Task<bool> ActualizarAsync(int id, Entrega obj);
        Task<bool> EliminarAsync(int id);
        Task<Entrega> ObtenerAsync(int id);
        Task<IQueryable<Entrega>> ObtenerTodoAsync();
        #endregion
    }
}
