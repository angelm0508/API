using System.Reflection;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>Fija por reflexión la tabla de permisos de los controllers del taller.</summary>
    public class TallerPermisosTests
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
            yield return new object[] { typeof(MecanicoController), "Mecanicos", new Dictionary<string, Accion>
            {
                ["ObtenerTodo"] = Accion.Ver, ["ObtenerPorCodigo"] = Accion.Ver, ["Crear"] = Accion.Agregar,
                ["Actualizar"] = Accion.Actualizar, ["Eliminar"] = Accion.Eliminar
            } };
            yield return new object[] { typeof(MotocicletaController), "Motocicletas", new Dictionary<string, Accion>
            {
                ["Buscar"] = Accion.Ver, ["Obtener"] = Accion.Ver, ["ObtenerHistorial"] = Accion.Ver, ["Crear"] = Accion.Agregar,
                ["Actualizar"] = Accion.Actualizar, ["Eliminar"] = Accion.Eliminar
            } };
            yield return new object[] { typeof(OrdenServicioController), "OrdenesServicio", new Dictionary<string, Accion>
            {
                ["Obtener"] = Accion.Ver, ["ObtenerTodoAsync"] = Accion.Ver, ["ObtenerLineas"] = Accion.Ver,
                ["InsertarAsync"] = Accion.Agregar, ["ActualizarAsync"] = Accion.Actualizar,
                ["CambiarEstadoAsync"] = Accion.Actualizar, ["CancelarAsync"] = Accion.Eliminar
            } };
            yield return new object[] { typeof(OrdenServicioDetalleController), "OrdenesServicio", new Dictionary<string, Accion>
            {
                ["InsertarAsync"] = Accion.Actualizar, ["ActualizarAsync"] = Accion.Actualizar, ["EliminarAsync"] = Accion.Actualizar
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
        [InlineData(typeof(MecanicoController))]
        [InlineData(typeof(MotocicletaController))]
        [InlineData(typeof(OrdenServicioController))]
        [InlineData(typeof(OrdenServicioDetalleController))]
        public void LosControllersDelTaller_ExigenAutenticacion(Type controller)
        {
            Assert.NotNull(controller.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        }
    }
}
