using API.Application.DTO;
using API.Application.DTO.moneda;

namespace API.Application.Interface
{
    public interface IMonedaApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(MonedaCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, MonedaActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<MonedaDTO>> ObtenerPorNombreAsync(string nombre);
        Task<Respuesta<MonedaDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<MonedaDTO>>> ObtenerAsync();
        Task<Respuesta<IEnumerable<MonedaDTO>>> ObtenerContengaNombreAsync(string nombre);
        Task<Respuesta<IEnumerable<MonedaDTO>>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
