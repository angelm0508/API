using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IMotocicletaDomain
    {
        #region async methods
        /// <summary>Registra la motocicleta y devuelve su Entry.</summary>
        Task<int> InsertarAsync(Motocicleta obj);
        Task<bool> ActualizarAsync(int entry, Motocicleta obj);
        /// <summary>Baja lógica: la motocicleta pasa a Activo='N' (nunca se borra, puede tener órdenes).</summary>
        Task<bool> EliminarAsync(int entry);
        Task<Motocicleta?> ObtenerAsync(int entry);
        Task<IEnumerable<Motocicleta>> BuscarAsync(string? codigoSn, string? placa);
        /// <summary>Órdenes de servicio de la motocicleta, de la más reciente a la más antigua.</summary>
        Task<IEnumerable<OrdenServicio>> ObtenerHistorialAsync(int entry);
        #endregion
    }
}
