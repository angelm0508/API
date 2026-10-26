using API.Application.DTO;
using API.Application.DTO.estadoCuenta;

namespace API.Application.Interface
{
    public interface IEstadoCuentaApplication
    {
        #region async methods
        /// <summary>Resultado verdadero con Dato nulo = el socio no existe (el controlador responde 404).</summary>
        Task<Respuesta<EstadoCuentaDTO>> ObtenerAsync(string codigoSn, DateTime? desde, DateTime? hasta);

        Task<Respuesta<IEnumerable<SaldoSocioDTO>>> ObtenerSaldosAsync(IEnumerable<string>? codigosSn);
        #endregion
    }
}
