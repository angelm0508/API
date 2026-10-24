using System.Reflection;
using API.Application.DTO;
using API.Application.DTO.pagoRecibido;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class SaldoFacturaControllerTests
    {
        private readonly Mock<ISaldoFacturaApplication> _app = new();

        private SaldoFacturaController Crear() => new(_app.Object);

        [Fact]
        public async Task SaldosFacturas_BadRequestYOk()
        {
            var ids = new List<int> { 1, 2 };
            var mala = new Respuesta<IEnumerable<SaldoFacturaDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<SaldoFacturaDTO>> { Resultado = true, Dato = new[] { new SaldoFacturaDTO { FacturaEntry = 1 } } };
            _app.SetupSequence(a => a.ObtenerAsync(ids)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().SaldosFacturas(ids)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().SaldosFacturas(ids)).Result).Value);
        }

        [Fact]
        public async Task SaldosNotasDebito_BadRequestYOk()
        {
            var ids = new List<int> { 5 };
            var mala = new Respuesta<IEnumerable<SaldoFacturaDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<SaldoFacturaDTO>> { Resultado = true, Dato = new[] { new SaldoFacturaDTO { FacturaEntry = 5 } } };
            _app.SetupSequence(a => a.ObtenerAsync(ids)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().SaldosNotasDebito(ids)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().SaldosNotasDebito(ids)).Result).Value);
        }

        [Fact]
        public async Task SaldosFacturas_CuerpoNulo_SeTrataComoListaVacia()
        {
            var vacia = new Respuesta<IEnumerable<SaldoFacturaDTO>> { Resultado = true, Dato = Array.Empty<SaldoFacturaDTO>() };
            _app.Setup(a => a.ObtenerAsync(It.Is<IEnumerable<int>>(e => !e.Any()))).ReturnsAsync(vacia);

            Assert.Same(vacia, Assert.IsType<OkObjectResult>((await Crear().SaldosFacturas(null)).Result).Value);
        }

        [Theory]
        [InlineData("SaldosFacturas", "Facturas", "api/Factura/Saldos")]
        [InlineData("SaldosNotasDebito", "NotasDebito", "api/NotaDebito/Saldos")]
        public void CadaAccion_ExigeVerEnSuModuloYSeExponeComoPost(string accion, string modulo, string ruta)
        {
            var campoModulo = typeof(RequierePermisoAttribute).GetField("_codigoModulo", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var campoAccion = typeof(RequierePermisoAttribute).GetField("_accion", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var metodo = typeof(SaldoFacturaController).GetMethod(accion)!;

            var permiso = metodo.GetCustomAttribute<RequierePermisoAttribute>()!;
            Assert.Equal(modulo, (string)campoModulo.GetValue(permiso)!);
            Assert.Equal(Accion.Ver, (Accion)campoAccion.GetValue(permiso)!);
            var http = Assert.IsAssignableFrom<HttpMethodAttribute>(metodo.GetCustomAttributes(typeof(HttpMethodAttribute), true).Single());
            Assert.Equal(new[] { "POST" }, http.HttpMethods.ToArray());
            Assert.Equal(ruta, http.Template);
        }
    }
}
