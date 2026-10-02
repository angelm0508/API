using System.Security.Claims;
using API.Domain.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Authorization
{
    /// <summary>
    /// Evaluación de permisos sobre los claims del JWT. Reutilizada por
    /// <see cref="RequierePermisoAttribute"/> y por los controllers que necesitan un chequeo
    /// adicional dentro de la acción (p. ej. cancelar un documento exige Eliminar).
    /// </summary>
    public static class PermisoUsuario
    {
        public const string ModuloUsuarios = "Usuarios";
        public const string MensajeDenegado = "No tenés permiso para realizar esta acción.";

        public static bool EsSuperUsuario(ClaimsPrincipal? user) =>
            user?.FindFirst("SuperUsuario")?.Value == "S";

        /// <summary>
        /// SuperUsuario siempre pasa. El módulo "Usuarios" es exclusivo de SuperUsuario
        /// (ningún claim "perm" lo habilita). Para el resto se busca la letra de la acción
        /// en el claim "perm" del módulo.
        /// </summary>
        public static bool Tiene(ClaimsPrincipal? user, string codigoModulo, Accion accion)
        {
            if (user is null) return false;
            if (EsSuperUsuario(user)) return true;
            if (codigoModulo == ModuloUsuarios) return false;

            var letra = accion switch
            {
                Accion.Ver => 'V',
                Accion.Agregar => 'A',
                Accion.Actualizar => 'U',
                Accion.Eliminar => 'E',
                _ => ' '
            };

            return user.FindAll("perm")
                .Select(c => c.Value.Split(':'))
                .Where(p => p.Length == 2 && p[0] == codigoModulo)
                .Any(p => p[1].Contains(letra));
        }

        public static ObjectResult Denegado() =>
            new ObjectResult(new API.Application.DTO.Respuesta<object> { Mensaje = MensajeDenegado })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
    }
}
