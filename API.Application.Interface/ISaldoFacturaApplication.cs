using API.Application.DTO;
using API.Application.DTO.pagoRecibido;

namespace API.Application.Interface
{
    public interface ISaldoFacturaApplication
    {
        #region async methods
        Task<Respuesta<IEnumerable<SaldoFacturaDTO>>> ObtenerAsync(IEnumerable<int> entries);
        #endregion
    }
}
