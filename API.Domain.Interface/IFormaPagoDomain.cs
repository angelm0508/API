using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IFormaPagoDomain
    {
        #region async methods
        Task<bool> InsertarAsync(FormaPago obj);
        Task<bool> ActualizarAsync(string codigo, FormaPago obj);
        /// <summary>Baja lógica: la forma de pago pasa a Activo='N' (nunca se borra, puede tener pagos).</summary>
        Task<bool> EliminarAsync(string codigo);
        Task<FormaPago?> ObtenerPorCodigoAsync(string codigo);
        Task<IQueryable<FormaPago>> ObtenerTodoAsync();
        #endregion
    }
}
