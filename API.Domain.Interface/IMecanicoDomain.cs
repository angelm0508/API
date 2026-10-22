using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IMecanicoDomain
    {
        #region async methods
        Task<bool> InsertarAsync(Mecanico obj);
        Task<bool> ActualizarAsync(string codigo, Mecanico obj);
        /// <summary>Baja lógica: el mecánico pasa a Activo='N' (nunca se borra, puede tener órdenes).</summary>
        Task<bool> EliminarAsync(string codigo);
        Task<Mecanico?> ObtenerPorCodigoAsync(string codigo);
        Task<IQueryable<Mecanico>> ObtenerTodoAsync();
        #endregion
    }
}
