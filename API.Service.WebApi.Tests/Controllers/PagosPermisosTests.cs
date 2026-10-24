using System.Reflection;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>Fija por reflexión la tabla de permisos de los controllers de pagos.</summary>
    public class PagosPermisosTests
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
            yield return new object[] { typeof(FormaPagoController), "FormasPago", new Dictionary<string, Accion>
            {
                ["ObtenerTodo"] = Accion.Ver, ["ObtenerPorCodigo"] = Accion.Ver, ["Crear"] = Accion.Agregar,
                ["Actualizar"] = Accion.Actualizar, ["Eliminar"] = Accion.Eliminar
            } };
            yield return new object[] { typeof(PagoRecibidoController), "PagosRecibidos", new Dictionary<string, Accion>
            {
                ["ObtenerTodo"] = Accion.Ver, ["Obtener"] = Accion.Ver, ["ObtenerFacturas"] = Accion.Ver, ["ObtenerMedios"] = Accion.Ver,
                ["ObtenerDocumentosPorCobrar"] = Accion.Agregar, ["Crear"] = Accion.Agregar, ["Cancelar"] = Accion.Eliminar
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

        [Theory]
        [InlineData(typeof(FormaPagoController))]
        [InlineData(typeof(PagoRecibidoController))]
        public void ElControllerExigeAutenticacion(Type controller)
        {
            Assert.NotNull(controller.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        }
    }
}
