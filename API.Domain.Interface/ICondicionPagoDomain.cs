using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface ICondicionPagoDomain
    {
        #region async methods
        Task<bool> InsertarAsync(CondicionPago obj);
        /// <summary>Actualiza nombre, días y activo (el código es inmutable). Los días solo afectan documentos futuros.</summary>
        Task<bool> ActualizarAsync(string codigo, CondicionPago obj);
        /// <summary>Baja lógica: la condición pasa a Activo='N' (nunca se borra, la usan socios y documentos). CON no se puede dar de baja.</summary>
        Task<bool> EliminarAsync(string codigo);
        Task<CondicionPago?> ObtenerPorCodigoAsync(string codigo);
        Task<IQueryable<CondicionPago>> ObtenerTodoAsync();
        /// <summary>
        /// Devuelve la condición si existe y está activa (para asignarla a un socio o a un documento nuevo).
        /// Lanza "La condición de pago {c} no existe." o "La condición de pago {c} está inactiva.".
        /// </summary>
        Task<CondicionPago?> ObtenerActivaAsync(string codigo);
        #endregion
    }
}
