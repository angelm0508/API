using API.Application.DTO;
using API.Application.DTO.almacen;

namespace API.Application.Interface
{
    public interface IAlmacenApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(AlmacenCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, AlmacenActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<AlmacenDTO>> ObtenerPorNombreAsync(string nombre);
        Task<Respuesta<AlmacenDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<AlmacenDTO>>> ObtenerAsync();
        Task<Respuesta<IEnumerable<AlmacenDTO>>> ObtenerContengaNombreAsync(string nombre);
        Task<Respuesta<IEnumerable<AlmacenDTO>>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
