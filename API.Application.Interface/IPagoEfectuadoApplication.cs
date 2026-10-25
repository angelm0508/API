using API.Application.DTO;
using API.Application.DTO.pagoEfectuado;

namespace API.Application.Interface
{
    public interface IPagoEfectuadoApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(PagoEfectuadoCrearDTO obj);
        Task<Respuesta<bool>> CancelarAsync(int id);
        Task<Respuesta<PagoEfectuadoDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<PagoEfectuadoDTO>>> ObtenerTodosAsync();
        Task<Respuesta<IEnumerable<PagoEfectuadoFacturaDTO>>> ObtenerFacturasAsync(int id);
        Task<Respuesta<IEnumerable<PagoEfectuadoMedioDTO>>> ObtenerMediosAsync(int id);
        Task<Respuesta<IEnumerable<DocumentoPorPagarDTO>>> ObtenerAbiertosAsync(string codigoSn);
        #endregion
    }
}
