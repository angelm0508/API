using System.Globalization;

namespace API.Domain.Interface
{
    /// <summary>
    /// Un documento nuevo supera el límite de crédito del cliente o el cliente tiene saldo vencido y está bloqueado.
    /// La capa de aplicación la traduce a un 409 con la posición de crédito (Resultado).
    /// </summary>
    public sealed class CreditoExcedidoException : Exception
    {
        public ResultadoCredito Resultado { get; }

        public string Codigo => Resultado.Motivo == "VENCIDO" ? "CLIENTE_CON_VENCIDO" : "LIMITE_CREDITO_EXCEDIDO";

        public CreditoExcedidoException(ResultadoCredito resultado, string mensaje) : base(mensaje)
        {
            Resultado = resultado;
        }

        /// <summary>Arma la excepción con el mensaje de usuario del motivo (cifras con 2 decimales, cultura invariante).</summary>
        public static CreditoExcedidoException Para(ResultadoCredito r, string codigoSn)
        {
            if (r.Permitido || r.Posicion is null)
                throw new InvalidOperationException("Solo un resultado de crédito rechazado y con posición puede convertirse en excepción.");

            var c = CultureInfo.InvariantCulture;
            var p = r.Posicion;
            var mensaje = r.Motivo == "VENCIDO"
                ? string.Format(c, "El cliente {0} tiene saldo vencido de {1:N2} y está bloqueado para nuevos documentos.", codigoSn, p.Vencido)
                : string.Format(c, "El cliente {0} excede su límite de crédito: límite {1:N2}, exposición {2:N2}, documento {3:N2}, exceso {4:N2}.",
                    codigoSn, p.Limite ?? 0m, p.Exposicion, r.Monto, r.Exceso);
            return new CreditoExcedidoException(r, mensaje);
        }
    }
}
