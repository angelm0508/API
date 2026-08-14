using API.Application.DTO;
using API.Application.DTO.articulo.grupo_medida_articulo;

namespace API.Application.Interface
{
    public interface IGrupoMedidaArticuloApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(GrupoMedidaArticuloCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, GrupoMedidaArticuloActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<GrupoMedidaArticuloDTO>> ObtenerAsync(int id);
        Task<Respuesta<GrupoMedidaArticuloDTO>> ObtenerAsync(string name);
        Task<Respuesta<IEnumerable<GrupoMedidaArticuloDTO>>> ObtenerContengaNombreAsync(string name);
        Task<Respuesta<IEnumerable<GrupoMedidaArticuloDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
