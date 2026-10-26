using API.Application.DTO;
using API.Application.DTO.estadoCuenta;
using API.Application.Interface;
using API.Domain.Interface;

namespace API.Application.Main
{
    public class EstadoCuentaApplication : IEstadoCuentaApplication
    {
        /// <summary>Mensaje que lanza IEstadoCuentaService cuando el socio no existe; se traduce a "no encontrado" (Dato nulo).</summary>
        public const string SocioNoExiste = "El socio de negocio no existe.";
        public const string DemasiadosSocios = "Demasiados socios en la consulta.";
        public const int MaximoSocios = 500;

        private readonly IEstadoCuentaService _estadoCuenta;
        private readonly ISaldoSocioService _saldos;

        public EstadoCuentaApplication(IEstadoCuentaService estadoCuenta, ISaldoSocioService saldos)
        {
            _estadoCuenta = estadoCuenta;
            _saldos = saldos;
        }

        #region async methods
        public async Task<Respuesta<EstadoCuentaDTO>> ObtenerAsync(string codigoSn, DateTime? desde, DateTime? hasta)
        {
            var respuesta = new Respuesta<EstadoCuentaDTO>();
            try
            {
                var estado = await _estadoCuenta.ObtenerAsync(codigoSn, desde, hasta);
                respuesta.Dato = new EstadoCuentaDTO
                {
                    CodigoSn = estado.Socio.Codigo,
                    NombreSn = estado.Socio.Nombre,
                    TipoSn = estado.Socio.TipoSn,
                    Desde = estado.Desde,
                    Hasta = estado.Hasta,
                    SaldoInicial = estado.SaldoInicial,
                    SaldoFinal = estado.SaldoFinal,
                    SaldoActual = estado.SaldoActual,
                    Vencido = estado.Vencido,
                    Movimientos = estado.Movimientos.Select(m => new MovimientoCuentaDTO
                    {
                        Fecha = m.Fecha,
                        Tipo = m.Tipo,
                        NumDoc = m.NumDoc,
                        Descripcion = m.Descripcion,
                        Cargo = m.Cargo,
                        Abono = m.Abono,
                        SaldoAcumulado = m.SaldoAcumulado,
                        Informativo = m.Informativo
                    }).ToList()
                };
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex) when (ex.Message == SocioNoExiste)
            {
                // Socio inexistente: no es un error de la consulta, el controlador lo traduce a 404.
                respuesta.Dato = null;
                respuesta.Resultado = true;
                respuesta.Mensaje = SocioNoExiste;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<SaldoSocioDTO>>> ObtenerSaldosAsync(IEnumerable<string>? codigosSn)
        {
            var respuesta = new Respuesta<IEnumerable<SaldoSocioDTO>>();
            try
            {
                var pedidos = (codigosSn ?? Enumerable.Empty<string>()).ToList();
                if (pedidos.Count > MaximoSocios)
                {
                    respuesta.Mensaje = DemasiadosSocios;
                    return respuesta;
                }

                var codigos = pedidos
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                IReadOnlyDictionary<string, SaldoSocio> saldos = codigos.Count == 0
                    ? new Dictionary<string, SaldoSocio>()
                    : await _saldos.ObtenerAsync(codigos);

                respuesta.Dato = saldos.Values
                    .OrderBy(s => s.CodigoSn, StringComparer.Ordinal)
                    .Select(s => new SaldoSocioDTO
                    {
                        CodigoSn = s.CodigoSn,
                        Tipo = s.Tipo,
                        Saldo = s.Saldo,
                        Vencido = s.Vencido,
                        DocumentosAbiertos = s.DocumentosAbiertos
                    }).ToList();
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion
    }
}
