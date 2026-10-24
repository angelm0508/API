using API.Application.DTO;
using API.Application.DTO.pagoRecibido;

namespace API.Application.Interface
{
    public interface IPagoRecibidoApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(PagoRecibidoCrearDTO obj);
        Task<Respuesta<bool>> CancelarAsync(int id);
        Task<Respuesta<PagoRecibidoDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<PagoRecibidoDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<PagoRecibidoFacturaDTO>>> ObtenerFacturasAsync(int id);
        Task<Respuesta<IEnumerable<PagoRecibidoMedioDTO>>> ObtenerMediosAsync(int id);
        Task<Respuesta<IEnumerable<DocumentoPorCobrarDTO>>> ObtenerDocumentosPorCobrarAsync(string codigoSn);
        #endregion
    }
}
