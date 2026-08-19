using API.Application.DTO;
using API.Application.DTO.departamento;

namespace API.Application.Interface
{
    public interface IDepartamentoApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(DepartamentoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, DepartamentoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<DepartamentoDTO>> ObtenerPorNombreAsync(string nombre);
        Task<Respuesta<DepartamentoDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<DepartamentoDTO>>> ObtenerAsync();
        Task<Respuesta<IEnumerable<DepartamentoDTO>>> ObtenerContengaNombreAsync(string nombre);
        Task<Respuesta<IEnumerable<DepartamentoDTO>>> ObtenerContengaCodigoAsync(string codigo);
        #endregion
    }
}
