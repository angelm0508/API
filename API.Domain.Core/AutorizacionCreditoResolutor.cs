using API.Domain.Interface;

namespace API.Domain.Core
{
    /// <summary>Decide qué hacer con el resultado de la verificación de crédito en los dominios de documentos de venta.</summary>
    internal static class AutorizacionCreditoResolutor
    {
        /// <summary>
        /// Resuelve qué hacer con el resultado de la verificación de crédito. Permitido: null (nada que registrar). Rechazado con
        /// autorización del contexto y usuario válido: el usuario que se registra. Rechazado sin autorización: lanza
        /// <see cref="CreditoExcedidoException"/>. Una autorización sin usuario se trata como no autorizada.
        /// </summary>
        public static string? Resolver(IAutorizacionCreditoContexto contexto, ResultadoCredito resultado, string? codigoSn)
        {
            if (resultado.Permitido)
                return null;

            var usuario = contexto.Usuario?.Trim();
            if (!contexto.Autorizada || string.IsNullOrEmpty(usuario))
                throw CreditoExcedidoException.Para(resultado, codigoSn ?? string.Empty);

            if (usuario.Length > 50)
                throw new Exception("El usuario que autoriza no es válido.");

            return usuario;
        }
    }
}
