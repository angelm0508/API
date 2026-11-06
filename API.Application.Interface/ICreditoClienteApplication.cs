using API.Application.DTO;
using API.Application.DTO.creditoCliente;

namespace API.Application.Interface
{
    public interface ICreditoClienteApplication
    {
        #region async methods
        /// <summary>Resultado verdadero con Dato nulo = el cliente no existe (el controlador responde 404).</summary>
        Task<Respuesta<PosicionCreditoDTO>> ObtenerPosicionAsync(string codigoSn);

        Task<Respuesta<IEnumerable<PosicionCreditoDTO>>> ObtenerPosicionesAsync(IEnumerable<string>? codigosSn);
        #endregion
    }
}
