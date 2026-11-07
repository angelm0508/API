using API.Application.DTO;
using API.Application.DTO.articulo.almacen;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class ArticuloAlmacenControllerTests
    {
        private readonly Mock<IArticuloAlmacenApplication> _app = new();

        private ArticuloAlmacenController Crear() => new(_app.Object);

        [Fact]
        public async Task Obtener_Ok_DevuelveLasFilas()
        {
            var buena = new Respuesta<IEnumerable<ArticuloAlmacenDTO>> { Resultado = true, Dato = new[] { new ArticuloAlmacenDTO { CodAlmacen = "01" } } };
            _app.Setup(a => a.ObtenerAsync("ART1")).ReturnsAsync(buena);

            var r = await Crear().Obtener("ART1");

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Obtener_ArticuloInexistente_DevuelveNotFound()
        {
            var mala = new Respuesta<IEnumerable<ArticuloAlmacenDTO>> { Resultado = false, Mensaje = "El artículo X no existe." };
            _app.Setup(a => a.ObtenerAsync("X")).ReturnsAsync(mala);

            var r = await Crear().Obtener("X");

            Assert.Same(mala, Assert.IsType<NotFoundObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Obtener_OtroError_DevuelveBadRequest()
        {
            var mala = new Respuesta<IEnumerable<ArticuloAlmacenDTO>> { Resultado = false, Mensaje = "falla" };
            _app.Setup(a => a.ObtenerAsync("ART1")).ReturnsAsync(mala);

            var r = await Crear().Obtener("ART1");

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Guardar_Ok_DevuelveOk()
        {
            var filas = new List<ArticuloAlmacenGuardarDTO> { new() { CodAlmacen = "01", Minimo = 1m } };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.GuardarAsync("ART1", filas)).ReturnsAsync(buena);

            var r = await Crear().Guardar("ART1", filas);

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Guardar_CuerpoNulo_SePasaTalCualALaAplicacion()
        {
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.GuardarAsync("ART1", null)).ReturnsAsync(buena);

            var r = await Crear().Guardar("ART1", null);

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
            _app.Verify(a => a.GuardarAsync("ART1", null), Times.Once);
        }

        [Fact]
        public async Task Guardar_ArticuloInexistente_DevuelveNotFound()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "El artículo X no existe." };
            _app.Setup(a => a.GuardarAsync("X", It.IsAny<IEnumerable<ArticuloAlmacenGuardarDTO>?>())).ReturnsAsync(mala);

            var r = await Crear().Guardar("X", new List<ArticuloAlmacenGuardarDTO>());

            Assert.Same(mala, Assert.IsType<NotFoundObjectResult>(r.Result).Value);
        }

        [Theory]
        [InlineData("El almacén 99 no existe.")]
        [InlineData("El almacén 01 está repetido en la lista.")]
        [InlineData("El stock mínimo del almacén 01 no puede ser negativo.")]
        [InlineData("El artículo ART1 está bloqueado en el almacén 01.")]
        public async Task Guardar_ErrorDeValidacion_DevuelveBadRequest(string mensaje)
        {
            // Solo «El artículo ... no existe.» es 404: un almacén inexistente u otra validación es 400.
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = mensaje };
            _app.Setup(a => a.GuardarAsync("ART1", It.IsAny<IEnumerable<ArticuloAlmacenGuardarDTO>?>())).ReturnsAsync(mala);

            var r = await Crear().Guardar("ART1", new List<ArticuloAlmacenGuardarDTO>());

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task BajoMinimo_OkYBadRequest()
        {
            var buena = new Respuesta<IEnumerable<AlmacenBajoMinimoDTO>> { Resultado = true, Dato = new[] { new AlmacenBajoMinimoDTO { CodArticulo = "A" } } };
            var mala = new Respuesta<IEnumerable<AlmacenBajoMinimoDTO>> { Resultado = false, Mensaje = "falla" };
            _app.SetupSequence(a => a.ObtenerBajoMinimoAsync()).ReturnsAsync(buena).ReturnsAsync(mala);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().BajoMinimo()).Result).Value);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().BajoMinimo()).Result).Value);
        }
    }
}
