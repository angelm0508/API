using System.Reflection;
using API.Application.DTO;
using API.Application.DTO.pagoEfectuado;
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
    public class SaldoFacturaCompraControllerTests
    {
        private readonly Mock<ISaldoFacturaCompraApplication> _app = new();

        private SaldoFacturaCompraController Crear() => new(_app.Object);

        [Fact]
        public async Task SaldosFacturasCompra_BadRequestYOk()
        {
            var ids = new List<int> { 1, 2 };
            var mala = new Respuesta<IEnumerable<SaldoFacturaCompraDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<SaldoFacturaCompraDTO>> { Resultado = true, Dato = new[] { new SaldoFacturaCompraDTO { FacturaCompraEntry = 1 } } };
            _app.SetupSequence(a => a.ObtenerAsync(ids)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().SaldosFacturasCompra(ids)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().SaldosFacturasCompra(ids)).Result).Value);
        }

        [Fact]
        public async Task SaldosNotasDebitoCompra_BadRequestYOk()
        {
            var ids = new List<int> { 5 };
            var mala = new Respuesta<IEnumerable<SaldoFacturaCompraDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<SaldoFacturaCompraDTO>> { Resultado = true, Dato = new[] { new SaldoFacturaCompraDTO { FacturaCompraEntry = 5 } } };
            _app.SetupSequence(a => a.ObtenerAsync(ids)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().SaldosNotasDebitoCompra(ids)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().SaldosNotasDebitoCompra(ids)).Result).Value);
        }

        [Fact]
        public async Task SaldosFacturasCompra_CuerpoNulo_SeTrataComoListaVacia()
        {
            var vacia = new Respuesta<IEnumerable<SaldoFacturaCompraDTO>> { Resultado = true, Dato = Array.Empty<SaldoFacturaCompraDTO>() };
            _app.Setup(a => a.ObtenerAsync(It.Is<IEnumerable<int>>(e => !e.Any()))).ReturnsAsync(vacia);

            Assert.Same(vacia, Assert.IsType<OkObjectResult>((await Crear().SaldosFacturasCompra(null)).Result).Value);
        }

        [Theory]
        [InlineData("SaldosFacturasCompra", "FacturasCompra", "~/api/FacturaCompra/Saldos")]
        [InlineData("SaldosNotasDebitoCompra", "NotasDebitoCompra", "~/api/NotaDebitoCompra/Saldos")]
        public void CadaAccion_ExigeVerEnSuModuloYSeExponeComoPost(string accion, string modulo, string ruta)
        {
            var campoModulo = typeof(RequierePermisoAttribute).GetField("_codigoModulo", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var campoAccion = typeof(RequierePermisoAttribute).GetField("_accion", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var metodo = typeof(SaldoFacturaCompraController).GetMethod(accion)!;

            var permiso = metodo.GetCustomAttribute<RequierePermisoAttribute>()!;
            Assert.Equal(modulo, (string)campoModulo.GetValue(permiso)!);
            Assert.Equal(Accion.Ver, (Accion)campoAccion.GetValue(permiso)!);
            var http = Assert.IsAssignableFrom<HttpMethodAttribute>(metodo.GetCustomAttributes(typeof(HttpMethodAttribute), true).Single());
            Assert.Equal(new[] { "POST" }, http.HttpMethods.ToArray());
            Assert.Equal(ruta, http.Template);
        }

        [Theory]
        [InlineData(typeof(FacturaCompraController))]
        [InlineData(typeof(NotaDebitoCompraController))]
        public void LaRutaSaldos_NoColisionaConLasAccionesDelControladorDeSuDocumento(Type controlador)
        {
            // Ninguna accion del controlador existente declara un POST con "Saldos".
            var plantillasPost = controlador.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true).Cast<HttpMethodAttribute>())
                .Where(h => h.HttpMethods.Contains("POST"))
                .Select(h => h.Template)
                .ToList();
            Assert.DoesNotContain(plantillasPost, t => t != null && t.Contains("Saldos"));
        }
    }
}
