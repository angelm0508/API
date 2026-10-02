using API.Domain.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Service.WebApi.Authorization
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RequierePermisoAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string _codigoModulo;
        private readonly Accion _accion;

        public RequierePermisoAttribute(string codigoModulo, Accion accion)
        {
            _codigoModulo = codigoModulo;
            _accion = accion;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user.FindFirst("SuperUsuario")?.Value == "S") return;

            var letra = _accion switch
            {
                Accion.Ver => 'V',
                Accion.Agregar => 'A',
                Accion.Actualizar => 'U',
                Accion.Eliminar => 'E',
                _ => ' '
            };

            var tienePermiso = user.FindAll("perm")
                .Select(c => c.Value.Split(':'))
                .Where(p => p.Length == 2 && p[0] == _codigoModulo)
                .Any(p => p[1].Contains(letra));

            if (!tienePermiso)
            {
                context.Result = new ObjectResult(new API.Application.DTO.Respuesta<object>
                {
                    Mensaje = "No tenés permiso para realizar esta acción."
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
