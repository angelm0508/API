using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IEntradaMercanciaDetalleDomain
    {
        #region async methods
        Task<int> InsertarAsync(EntradaMercanciaDetalle obj);
        Task<bool> ActualizarAsync(int entry, int noLinea, EntradaMercanciaDetalle obj);
        Task<bool> EliminarAsync(int entry, int noLinea);
        Task<EntradaMercanciaDetalle> ObtenerAsync(int entry, int noLinea);
        Task<IQueryable<EntradaMercanciaDetalle>> ObtenerTodoAsync();
        Task<IEnumerable<EntradaMercanciaDetalle>> ObtenerPorEntradaMercanciaAsync(int entry);
        #endregion
    }
}
