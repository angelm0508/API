using API.Application.DTO;
using API.Application.DTO.pagoEfectuado;

namespace API.Application.Interface
{
    public interface ISaldoFacturaCompraApplication
    {
        #region async methods
        Task<Respuesta<IEnumerable<SaldoFacturaCompraDTO>>> ObtenerAsync(IEnumerable<int> entries);
        #endregion
    }
}
