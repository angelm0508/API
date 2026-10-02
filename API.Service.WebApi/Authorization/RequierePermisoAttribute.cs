using API.Domain.Interface;
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
            if (!PermisoUsuario.Tiene(context.HttpContext.User, _codigoModulo, _accion))
                context.Result = PermisoUsuario.Denegado();
        }
    }
}
