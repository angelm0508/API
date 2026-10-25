using System.Reflection;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>Fija por reflexión la tabla de permisos del controller de pagos efectuados.</summary>
    public class PagosEfectuadosPermisosTests
    {
        private static readonly FieldInfo CampoModulo =
            typeof(RequierePermisoAttribute).GetField("_codigoModulo", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly FieldInfo CampoAccion =
            typeof(RequierePermisoAttribute).GetField("_accion", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static IEnumerable<MethodInfo> Acciones(Type controller) =>
            controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true).Any());

        private static (string Modulo, Accion Accion) Permiso(MethodInfo metodo)
        {
            var attr = metodo.GetCustomAttribute<RequierePermisoAttribute>();
            Assert.NotNull(attr);
            return ((string)CampoModulo.GetValue(attr)!, (Accion)CampoAccion.GetValue(attr)!);
        }

        public static IEnumerable<object[]> Casos()
        {
            yield return new object[] { typeof(PagoEfectuadoController), "PagosEfectuados", new Dictionary<string, Accion>
            {
                ["ObtenerTodos"] = Accion.Ver, ["Obtener"] = Accion.Ver, ["ObtenerFacturas"] = Accion.Ver, ["ObtenerMedios"] = Accion.Ver,
                ["ObtenerAbiertos"] = Accion.Agregar, ["Crear"] = Accion.Agregar, ["Cancelar"] = Accion.Eliminar
            } };
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void TablaDePermisos_EsLaEsperada(Type controller, string modulo, Dictionary<string, Accion> esperado)
        {
            var real = Acciones(controller).ToDictionary(m => m.Name, m => Permiso(m));

            Assert.Equal(esperado.Keys.OrderBy(k => k), real.Keys.OrderBy(k => k));
            foreach (var (nombre, accion) in esperado)
            {
                Assert.Equal(modulo, real[nombre].Modulo);
                Assert.True(accion == real[nombre].Accion, $"{controller.Name}.{nombre}: se esperaba {accion} y es {real[nombre].Accion}");
            }
        }

        [Fact]
        public void ElControllerExigeAutenticacion()
        {
            Assert.NotNull(typeof(PagoEfectuadoController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        }

        [Theory]
        [InlineData("ObtenerTodos", "")]
        [InlineData("Obtener", "{id:int}")]
        [InlineData("ObtenerFacturas", "{id:int}/Facturas")]
        [InlineData("ObtenerMedios", "{id:int}/Medios")]
        [InlineData("ObtenerAbiertos", "FacturasAbiertas/{codigoSn}")]
        [InlineData("Crear", "")]
        [InlineData("Cancelar", "{id:int}/Cancelar")]
        public void LasRutasSonLasDeLaEspecificacion(string accion, string plantilla)
        {
            var metodo = typeof(PagoEfectuadoController).GetMethod(accion)!;
            var http = metodo.GetCustomAttributes<HttpMethodAttribute>().Single();
            Assert.Equal(plantilla, http.Template ?? "");
        }
    }
}
