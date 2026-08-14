using API.Application.DTO;
using API.Application.DTO.articulo.medida_articulo;

namespace API.Application.Interface
{
    public interface IMedidaArticuloApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(MedidaArticuloCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, MedidaArticuloActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<MedidaArticuloDTO>> ObtenerAsync(int id);
        Task<Respuesta<MedidaArticuloDTO>> ObtenerAsync(string codigo);
        Task<Respuesta<IEnumerable<MedidaArticuloDTO>>> ObtenerContengaNombreAsync(string name);
        Task<Respuesta<IEnumerable<MedidaArticuloDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
