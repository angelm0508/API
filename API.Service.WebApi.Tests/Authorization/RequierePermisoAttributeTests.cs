using System.Security.Claims;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace API.Service.WebApi.Tests.Authorization
{
    public class RequierePermisoAttributeTests
    {
        private static AuthorizationFilterContext Contexto(params Claim[] claims)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }

        [Fact]
        public void OnAuthorization_SuperUsuario_NoBloquea()
        {
            var ctx = Contexto(new Claim("SuperUsuario", "S"));
            new RequierePermisoAttribute("Articulos", Accion.Eliminar).OnAuthorization(ctx);

            Assert.Null(ctx.Result);
        }

        [Fact]
        public void OnAuthorization_ConLaLetraPresente_NoBloquea()
        {
            var ctx = Contexto(new Claim("SuperUsuario", "N"), new Claim("perm", "Articulos:VAUE"));
            new RequierePermisoAttribute("Articulos", Accion.Agregar).OnAuthorization(ctx);

            Assert.Null(ctx.Result);
        }

        [Fact]
        public void OnAuthorization_SinLaLetra_Bloquea403()
        {
            var ctx = Contexto(new Claim("SuperUsuario", "N"), new Claim("perm", "Articulos:V"));
            new RequierePermisoAttribute("Articulos", Accion.Eliminar).OnAuthorization(ctx);

            var resultado = Assert.IsType<ObjectResult>(ctx.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, resultado.StatusCode);
        }

        [Fact]
        public void OnAuthorization_SinClaimParaEseModulo_Bloquea403()
        {
            var ctx = Contexto(new Claim("SuperUsuario", "N"), new Claim("perm", "Almacenes:VAUE"));
            new RequierePermisoAttribute("Articulos", Accion.Ver).OnAuthorization(ctx);

            Assert.IsType<ObjectResult>(ctx.Result);
        }

        [Theory]
        [InlineData(Accion.Ver)]
        [InlineData(Accion.Agregar)]
        [InlineData(Accion.Actualizar)]
        [InlineData(Accion.Eliminar)]
        public void OnAuthorization_ModuloUsuarios_NoSuperUsuarioConClaimPerm_Bloquea403(Accion accion)
        {
            var ctx = Contexto(new Claim("SuperUsuario", "N"), new Claim("perm", "Usuarios:VAUE"));
            new RequierePermisoAttribute("Usuarios", accion).OnAuthorization(ctx);

            var resultado = Assert.IsType<ObjectResult>(ctx.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, resultado.StatusCode);
        }

        [Fact]
        public void OnAuthorization_ModuloUsuarios_SuperUsuario_NoBloquea()
        {
            var ctx = Contexto(new Claim("SuperUsuario", "S"));
            new RequierePermisoAttribute("Usuarios", Accion.Actualizar).OnAuthorization(ctx);

            Assert.Null(ctx.Result);
        }

        [Fact]
        public void OnAuthorization_SinClaimSuperUsuario_SeTrataComoN()
        {
            var ctx = Contexto(new Claim("perm", "Articulos:V"));
            new RequierePermisoAttribute("Articulos", Accion.Ver).OnAuthorization(ctx);

            Assert.Null(ctx.Result);
        }
    }
}
