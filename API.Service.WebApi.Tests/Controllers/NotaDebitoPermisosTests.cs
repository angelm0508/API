using System.Reflection;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>
    /// Fija por reflexion la tabla de permisos de los controllers de nota de debito.
    /// Los endpoints de factura base solo los usa el formulario de creacion, por eso exigen Agregar
    /// (un usuario con solo Ver no debe poder leer facturas ajenas).
    /// </summary>
    public class NotaDebitoPermisosTests
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

        public static IEnumerable<object[]> Controllers() =>
            Casos().Select(c => new[] { c[0] });

        public static IEnumerable<object[]> Casos()
        {
            yield return new object[] { typeof(NotaDebitoController), "NotasDebito", "ObtenerFacturasBase", "ObtenerFacturaBase" };
            yield return new object[] { typeof(NotaDebitoCompraController), "NotasDebitoCompra", "ObtenerFacturasCompraBase", "ObtenerFacturaCompraBase" };
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void TablaDePermisos_EsLaEsperada(Type controller, string modulo, string facturasBase, string facturaBase)
        {
            var esperado = new Dictionary<string, Accion>
            {
                ["Obtener"] = Accion.Ver,
                ["ObtenerTodoAsync"] = Accion.Ver,
                ["ObtenerLineas"] = Accion.Ver,
                [facturasBase] = Accion.Agregar,
                [facturaBase] = Accion.Agregar,
                ["InsertarAsync"] = Accion.Agregar,
                ["ActualizarAsync"] = Accion.Actualizar,
                ["EliminarAsync"] = Accion.Eliminar,
            };

            var real = Acciones(controller).ToDictionary(m => m.Name, m => Permiso(m));

            Assert.Equal(esperado.Keys.OrderBy(k => k), real.Keys.OrderBy(k => k));
            foreach (var (nombre, accion) in esperado)
            {
                Assert.Equal(modulo, real[nombre].Modulo);
                Assert.True(accion == real[nombre].Accion, $"{controller.Name}.{nombre}: se esperaba {accion} y es {real[nombre].Accion}");
            }
        }

        [Theory]
        [MemberData(nameof(Controllers))]
        public void NingunaAccionPublicaCarecedeRequierePermiso(Type controller)
        {
            var publicos = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .ToList();

            Assert.NotEmpty(publicos);
            var sinAtributo = publicos
                .Where(m => m.GetCustomAttribute<RequierePermisoAttribute>() == null)
                .Select(m => m.Name)
                .ToList();

            Assert.True(sinAtributo.Count == 0, $"{controller.Name} tiene acciones sin RequierePermiso: {string.Join(", ", sinAtributo)}");
        }
    }
}
