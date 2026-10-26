using System.Reflection;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>Fija por reflexión permisos y rutas del controller de estado de cuenta (el 403 lo produce el atributo de permiso).</summary>
    public class EstadoCuentaPermisosTests
    {
        private static readonly FieldInfo CampoModulo =
            typeof(RequierePermisoAttribute).GetField("_codigoModulo", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly FieldInfo CampoAccion =
            typeof(RequierePermisoAttribute).GetField("_accion", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static IEnumerable<MethodInfo> Acciones() =>
            typeof(EstadoCuentaController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true).Any());

        [Fact]
        public void TablaDePermisos_TodasLasAccionesExigenVerEstadoCuenta()
        {
            var acciones = Acciones().ToList();

            Assert.Equal(new[] { "Obtener", "Saldos" }, acciones.Select(m => m.Name).OrderBy(n => n));
            foreach (var metodo in acciones)
            {
                var attr = metodo.GetCustomAttribute<RequierePermisoAttribute>();
                Assert.NotNull(attr);
                Assert.Equal("EstadoCuenta", (string)CampoModulo.GetValue(attr)!);
                Assert.True(Accion.Ver == (Accion)CampoAccion.GetValue(attr)!, $"{metodo.Name}: se esperaba Ver");
            }
        }

        [Fact]
        public void ElControllerExigeAutenticacion()
        {
            Assert.NotNull(typeof(EstadoCuentaController).GetCustomAttribute<AuthorizeAttribute>());
        }

        [Theory]
        [InlineData("Obtener", "GET", "~/api/EstadoCuenta/{codigoSn}")]
        [InlineData("Saldos", "POST", "~/api/EstadoCuenta/Saldos")]
        public void LasRutasSonAbsolutasYElVerboEsElEsperado(string accion, string verbo, string ruta)
        {
            var metodo = typeof(EstadoCuentaController).GetMethod(accion)!;
            var http = Assert.IsAssignableFrom<HttpMethodAttribute>(metodo.GetCustomAttributes(typeof(HttpMethodAttribute), true).Single());

            Assert.Equal(new[] { verbo }, http.HttpMethods.ToArray());
            Assert.Equal(ruta, http.Template);
        }
    }
}
