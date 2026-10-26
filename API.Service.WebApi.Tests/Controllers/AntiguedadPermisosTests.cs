using System.Reflection;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>
    /// Fija por reflexión permisos y rutas del controller de antigüedad. Cada acción lleva su propio permiso para que
    /// un usuario con solo AntiguedadCobrar no pueda leer lo de pagar y viceversa (el 403 lo produce el atributo).
    /// </summary>
    public class AntiguedadPermisosTests
    {
        private static readonly FieldInfo CampoModulo =
            typeof(RequierePermisoAttribute).GetField("_codigoModulo", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly FieldInfo CampoAccion =
            typeof(RequierePermisoAttribute).GetField("_accion", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static IEnumerable<MethodInfo> Acciones() =>
            typeof(AntiguedadController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true).Any());

        [Fact]
        public void ElControllerTieneExactamenteCuatroAcciones()
        {
            Assert.Equal(
                new[] { "DetalleCobrar", "DetallePagar", "ObtenerCobrar", "ObtenerPagar" },
                Acciones().Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
        }

        [Theory]
        [InlineData("ObtenerCobrar", "AntiguedadCobrar", "GET", "~/api/Antiguedad/Cobrar")]
        [InlineData("DetalleCobrar", "AntiguedadCobrar", "GET", "~/api/Antiguedad/Cobrar/{codigoSn}")]
        [InlineData("ObtenerPagar", "AntiguedadPagar", "GET", "~/api/Antiguedad/Pagar")]
        [InlineData("DetallePagar", "AntiguedadPagar", "GET", "~/api/Antiguedad/Pagar/{codigoSn}")]
        public void CadaAccionExigeVerSuModuloYTieneRutaAbsoluta(string accion, string modulo, string verbo, string ruta)
        {
            var metodo = typeof(AntiguedadController).GetMethod(accion)!;

            var permisos = metodo.GetCustomAttributes<RequierePermisoAttribute>(false).ToList();
            var attr = Assert.Single(permisos);
            Assert.Equal(modulo, (string)CampoModulo.GetValue(attr)!);
            Assert.True(Accion.Ver == (Accion)CampoAccion.GetValue(attr)!, $"{accion}: se esperaba Ver");

            var http = Assert.IsAssignableFrom<HttpMethodAttribute>(metodo.GetCustomAttributes(typeof(HttpMethodAttribute), true).Single());
            Assert.Equal(new[] { verbo }, http.HttpMethods.ToArray());
            Assert.Equal(ruta, http.Template);
        }

        [Fact]
        public void NoHayPermisoANivelDeClaseQueMezcleModulos()
        {
            Assert.Empty(typeof(AntiguedadController).GetCustomAttributes<RequierePermisoAttribute>(true));
            Assert.Null(typeof(AntiguedadController).GetCustomAttribute<RouteAttribute>(true));
        }

        [Fact]
        public void ElControllerExigeAutenticacion()
        {
            Assert.NotNull(typeof(AntiguedadController).GetCustomAttribute<AuthorizeAttribute>());
        }
    }
}
