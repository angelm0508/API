using API.Application.DTO;
using API.Application.DTO.facturaCompra;

namespace API.Application.Interface
{
    public interface IFacturaCompraApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(FacturaCompraCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, FacturaCompraActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<FacturaCompraDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<FacturaCompraDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
