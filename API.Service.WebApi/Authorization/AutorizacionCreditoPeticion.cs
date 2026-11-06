using System.Security.Claims;
using API.Domain.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Authorization
{
    /// <summary>
    /// Reglas que comparten las acciones de creación de Pedido, Entrega y Factura para autorizar excesos de crédito y
    /// para registrar un pago inmediato. Devuelven el 403 a responder, o null si la acción puede continuar.
    /// </summary>
    public static class AutorizacionCreditoPeticion
    {
        public const string ModuloAutorizar = "AutorizarExcesoCredito";
        public const string MensajeSinPermisoAutorizar = "No tenés permiso para autorizar excesos de crédito.";
        public const string MensajeSinPermisoPagos = "No tenés permiso para registrar pagos.";

        /// <summary>
        /// Si el usuario pidió autorizar el exceso: exige AutorizarExcesoCredito / Ver (403 si falta) y deja el contexto de la
        /// petición autorizado con el nombre de usuario del token (claim Name). Si no lo pidió, no toca el contexto.
        /// </summary>
        public static ObjectResult? Aplicar(ClaimsPrincipal? user, bool autorizarExceso, IAutorizacionCreditoContexto contexto)
        {
            if (!autorizarExceso)
                return null;

            if (!PermisoUsuario.Tiene(user, ModuloAutorizar, Accion.Ver))
                return Denegado(MensajeSinPermisoAutorizar);

            contexto.Autorizada = true;
            contexto.Usuario = user?.FindFirst(ClaimTypes.Name)?.Value;
            return null;
        }

        /// <summary>Un pago inmediato positivo exige PagosRecibidos / Agregar (403 «No tenés permiso para registrar pagos.»).</summary>
        public static ObjectResult? ValidarPagoInmediato(ClaimsPrincipal? user, decimal? pagoInmediato)
        {
            if (pagoInmediato > 0m && !PermisoUsuario.Tiene(user, "PagosRecibidos", Accion.Agregar))
                return Denegado(MensajeSinPermisoPagos);
            return null;
        }

        private static ObjectResult Denegado(string mensaje) =>
            new(new API.Application.DTO.Respuesta<object> { Mensaje = mensaje })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
    }
}
