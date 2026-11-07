using System.Reflection;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>Fija por reflexión permisos y rutas del controller de stock por almacén (el 403 lo produce el atributo de permiso).</summary>
    public class ArticuloAlmacenPermisosTests
    {
        private static readonly FieldInfo CampoModulo =
            typeof(RequierePermisoAttribute).GetField("_codigoModulo", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly FieldInfo CampoAccion =
            typeof(RequierePermisoAttribute).GetField("_accion", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static IEnumerable<MethodInfo> MetodosPublicos() =>
            typeof(ArticuloAlmacenController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        [Theory]
        [InlineData("Obtener", Accion.Ver)]
        [InlineData("Guardar", Accion.Actualizar)]
        [InlineData("BajoMinimo", Accion.Ver)]
        public void TablaDePermisos_ModuloArticulosYAccionEsperada(string metodo, Accion esperada)
        {
            var attr = typeof(ArticuloAlmacenController).GetMethod(metodo)!.GetCustomAttribute<RequierePermisoAttribute>();

            Assert.NotNull(attr);
            Assert.Equal("Articulos", (string)CampoModulo.GetValue(attr)!);
            Assert.True(esperada == (Accion)CampoAccion.GetValue(attr)!, $"{metodo}: se esperaba {esperada}");
        }

        [Fact]
        public void NoHayOtrosMetodosPublicosSinAtributoDePermiso()
        {
            var publicos = MetodosPublicos().ToList();

            Assert.Equal(new[] { "BajoMinimo", "Guardar", "Obtener" }, publicos.Select(m => m.Name).OrderBy(n => n));
            Assert.All(publicos, m => Assert.NotNull(m.GetCustomAttribute<RequierePermisoAttribute>()));
        }

        [Fact]
        public void ElPermisoVaPorAccion_NoAParteDeLaClase()
        {
            Assert.Null(typeof(ArticuloAlmacenController).GetCustomAttribute<RequierePermisoAttribute>());
        }

        [Fact]
        public void ElControllerExigeAutenticacionYCuelgaDeApiArticulo()
        {
            Assert.NotNull(typeof(ArticuloAlmacenController).GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(typeof(ArticuloAlmacenController).GetCustomAttribute<ApiControllerAttribute>());
            Assert.Equal("api/Articulo", typeof(ArticuloAlmacenController).GetCustomAttribute<RouteAttribute>()!.Template);
        }

        [Theory]
        [InlineData("Obtener", "GET", "{codigo}/Almacenes")]
        [InlineData("Guardar", "PUT", "{codigo}/Almacenes")]
        [InlineData("BajoMinimo", "GET", "Almacenes/BajoMinimo")]
        public void LasRutasYElVerboSonLosEsperados(string accion, string verbo, string ruta)
        {
            var metodo = typeof(ArticuloAlmacenController).GetMethod(accion)!;
            var http = Assert.IsAssignableFrom<HttpMethodAttribute>(metodo.GetCustomAttributes(typeof(HttpMethodAttribute), true).Single());

            Assert.Equal(new[] { verbo }, http.HttpMethods.ToArray());
            Assert.Equal(ruta, http.Template);
        }

        private static IEnumerable<(string Verbo, string Plantilla)> Plantillas(Type controller) =>
            controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true).Cast<HttpMethodAttribute>())
                .Select(h => (h.HttpMethods.First(), "api/Articulo/" + h.Template));

        private static bool Coincide(string plantilla, string url) =>
            new TemplateMatcher(TemplateParser.Parse(plantilla), new RouteValueDictionary())
                .TryMatch(new PathString("/" + url), new RouteValueDictionary());

        [Theory]
        [InlineData("api/Articulo/Almacenes/BajoMinimo", "GET", "api/Articulo/Almacenes/BajoMinimo")]
        [InlineData("api/Articulo/ART1/Almacenes", "GET", "api/Articulo/{codigo}/Almacenes")]
        [InlineData("api/Articulo/ART1/Almacenes", "PUT", "api/Articulo/{codigo}/Almacenes")]
        public void CadaUrlDelControllerNuevoCoincideConUnaSolaPlantillaDeLosDosControllers(string url, string verbo, string esperada)
        {
            var todas = Plantillas(typeof(ArticuloController)).Concat(Plantillas(typeof(ArticuloAlmacenController)));

            var coincidentes = todas.Where(p => p.Verbo == verbo && Coincide(p.Plantilla, url)).Select(p => p.Plantilla).ToList();

            Assert.Equal(new[] { esperada }, coincidentes);
        }

        [Theory]
        [InlineData("api/Articulo/ART1")]
        [InlineData("api/Articulo/ART1/Precio")]
        [InlineData("api/Articulo/Nombre/Aceite")]
        [InlineData("api/Articulo/ContengaNombre/Aceite")]
        [InlineData("api/Articulo/ContengaCodigo/AC")]
        public void LasRutasExistentesDeArticuloControllerNoLasCapturanLasNuevas(string url)
        {
            var nuevas = Plantillas(typeof(ArticuloAlmacenController));

            Assert.DoesNotContain(nuevas, p => Coincide(p.Plantilla, url));
        }
    }
}
