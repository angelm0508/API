using API.Application.DTO;
using API.Application.DTO.creditoCliente;
using API.Application.Interface;
using API.Domain.Interface;

namespace API.Application.Main
{
    public class CreditoClienteApplication : ICreditoClienteApplication
    {
        public const string ClienteNoExiste = "El cliente no existe.";
        public const string DemasiadosSocios = "Demasiados socios en la consulta.";
        public const int MaximoSocios = 500;

        private readonly ICreditoClienteService _credito;

        public CreditoClienteApplication(ICreditoClienteService credito)
        {
            _credito = credito;
        }

        #region async methods
        public async Task<Respuesta<PosicionCreditoDTO>> ObtenerPosicionAsync(string codigoSn)
        {
            var respuesta = new Respuesta<PosicionCreditoDTO>();
            try
            {
                var posicion = await _credito.ObtenerPosicionAsync(codigoSn);
                respuesta.Resultado = true;
                if (posicion is null)
                {
                    // Cliente inexistente: no es un error de la consulta, el controlador lo traduce a 404.
                    respuesta.Dato = null!;
                    respuesta.Mensaje = ClienteNoExiste;
                }
                else
                {
                    respuesta.Dato = Mapear(posicion);
                    respuesta.Mensaje = "Consulta realizada correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Resultado = false;
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PosicionCreditoDTO>>> ObtenerPosicionesAsync(IEnumerable<string>? codigosSn)
        {
            var respuesta = new Respuesta<IEnumerable<PosicionCreditoDTO>>();
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

                IReadOnlyDictionary<string, PosicionCredito> posiciones = codigos.Count == 0
                    ? new Dictionary<string, PosicionCredito>()
                    : await _credito.ObtenerPosicionesAsync(codigos);

                respuesta.Dato = posiciones.Values
                    .OrderBy(p => p.CodigoSn, StringComparer.Ordinal)
                    .Select(Mapear)
                    .ToList();
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

        /// <summary>
        /// Arma la respuesta 409 de un alta rechazada por el control de crédito: Resultado falso, el mensaje de la excepción,
        /// Dato por defecto y la posición/cifras en <see cref="RespuestaCredito{T}.Credito"/>.
        /// </summary>
        public static RespuestaCredito<T> RespuestaExceso<T>(CreditoExcedidoException ex)
        {
            var r = ex.Resultado;
            var p = r.Posicion;
            return new RespuestaCredito<T>
            {
                Resultado = false,
                Mensaje = ex.Message,
                Credito = new CreditoExcedidoDTO
                {
                    Codigo = ex.Codigo,
                    Limite = p?.Limite,
                    Saldo = p?.Saldo ?? 0m,
                    Comprometido = p?.Comprometido ?? 0m,
                    Vencido = p?.Vencido ?? 0m,
                    Monto = r.Monto,
                    Exceso = r.Exceso,
                    Disponible = p?.Disponible
                }
            };
        }

        private static PosicionCreditoDTO Mapear(PosicionCredito p) => new()
        {
            CodigoSn = p.CodigoSn,
            Limite = p.Limite,
            BloquearSiVencido = p.BloquearSiVencido,
            Saldo = p.Saldo,
            Vencido = p.Vencido,
            Comprometido = p.Comprometido,
            Exposicion = p.Exposicion,
            Disponible = p.Disponible,
            PorcentajeUso = p.Limite is > 0m
                ? Math.Round(p.Exposicion / p.Limite.Value * 100m, 1, MidpointRounding.AwayFromZero)
                : null
        };
    }
}
