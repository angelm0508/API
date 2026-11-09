using System.Reflection;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>Fija por reflexión la forma del controller: el permiso depende del reporte, así que se evalúa dentro de cada acción.</summary>
    public class ReportePermisosTests
    {
        private static IEnumerable<MethodInfo> MetodosPublicos() =>
            typeof(ReporteController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        [Fact]
        public void ElControllerExigeAutenticacionYCuelgaDeApiReporte()
        {
            Assert.NotNull(typeof(ReporteController).GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(typeof(ReporteController).GetCustomAttribute<ApiControllerAttribute>());
            Assert.Equal("api/Reporte", typeof(ReporteController).GetCustomAttribute<RouteAttribute>()!.Template);
        }

        [Fact]
        public void NingunaAccionUsaElAtributoDePermisoPorModuloFijo()
        {
            Assert.Null(typeof(ReporteController).GetCustomAttribute<RequierePermisoAttribute>());
            Assert.All(MetodosPublicos(), m => Assert.Null(m.GetCustomAttribute<RequierePermisoAttribute>()));
        }

        [Theory]
        [InlineData("Catalogo", "GET", "")]
        [InlineData("Definicion", "GET", "{codigo}")]
        [InlineData("Ejecutar", "POST", "{codigo}/ejecutar")]
        [InlineData("Exportar", "POST", "{codigo}/exportar")]
        public void LasRutasYElVerboSonLosEsperados(string accion, string verbo, string ruta)
        {
            var metodo = typeof(ReporteController).GetMethod(accion)!;
            var http = metodo.GetCustomAttributes().OfType<HttpMethodAttribute>().Single();

            Assert.Equal(verbo, http.HttpMethods.Single());
            Assert.Equal(ruta, http.Template ?? "");
        }
    }
}
