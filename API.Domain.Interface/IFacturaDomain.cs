using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IFacturaDomain
    {
        #region async methods
        Task<int> InsertarAsync(Factura obj);
        Task<bool> ActualizarAsync(int id, Factura obj);
        Task<bool> EliminarAsync(int id);
        Task<Factura> ObtenerAsync(int id);
        Task<IQueryable<Factura>> ObtenerTodoAsync();
        #endregion
    }
}
