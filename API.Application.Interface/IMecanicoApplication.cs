using API.Application.DTO;
using API.Application.DTO.mecanico;

namespace API.Application.Interface
{
    public interface IMecanicoApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(MecanicoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, MecanicoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<MecanicoDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<MecanicoDTO>>> ObtenerAsync();
        #endregion
    }
}
