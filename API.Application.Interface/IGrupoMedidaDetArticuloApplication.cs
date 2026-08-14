using API.Application.DTO;
using API.Application.DTO.articulo.grupo_medida_det_articulo;

namespace API.Application.Interface
{
    public interface IGrupoMedidaDetArticuloApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(GrupoMedidaDetArticuloCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, GrupoMedidaDetArticuloActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<GrupoMedidaDetArticuloDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<GrupoMedidaDetArticuloDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
