using API.Application.DTO;
using API.Application.DTO.motocicleta;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class MotocicletaControllerTests
    {
        private readonly Mock<IMotocicletaApplication> _app = new();

        private MotocicletaController Crear() => new(_app.Object);

        [Fact]
        public async Task Buscar_PasaLosFiltrosYDevuelveOk()
        {
            var buena = new Respuesta<IEnumerable<MotocicletaDTO>> { Resultado = true, Dato = new[] { new MotocicletaDTO() } };
            _app.Setup(a => a.BuscarAsync("CL-1", "M12")).ReturnsAsync(buena);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Buscar("CL-1", "M12")).Result).Value);
        }

        [Fact]
        public async Task Buscar_BadRequest_CuandoResultadoEsFalso()
        {
            var mala = new Respuesta<IEnumerable<MotocicletaDTO>> { Resultado = false, Mensaje = "error" };
            _app.Setup(a => a.BuscarAsync(null, null)).ReturnsAsync(mala);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Buscar(null, null)).Result).Value);
        }

        [Fact]
        public async Task Obtener_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(new Respuesta<MotocicletaDTO> { Resultado = true, Dato = null! });

            var notFound = Assert.IsType<NotFoundObjectResult>((await Crear().Obtener(9)).Result);
            Assert.Equal("La motocicleta no se encontró.", Assert.IsType<Respuesta<MotocicletaDTO>>(notFound.Value).Mensaje);
        }

        [Fact]
        public async Task Crear_BadRequestYOk()
        {
            var mala = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<int> { Resultado = true, Dato = 7 };
            var dto = new MotocicletaCrearDTO { CodigoSn = "CL-1", Placa = "M123ABC" };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Crear(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Crear(dto)).Result).Value);
        }

        [Fact]
        public async Task Actualizar_NotFound_YOk()
        {
            var dto = new MotocicletaActualizarDTO { CodigoSn = "CL-1", Placa = "M123ABC" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.SetupSequence(a => a.ObtenerAsync(5))
                .ReturnsAsync(new Respuesta<MotocicletaDTO> { Resultado = true, Dato = null! })
                .ReturnsAsync(new Respuesta<MotocicletaDTO> { Resultado = true, Dato = new MotocicletaDTO { Entry = 5 } });
            _app.Setup(a => a.ActualizarAsync(5, dto)).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().Actualizar(5, dto)).Result);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Actualizar(5, dto)).Result).Value);
        }

        [Fact]
        public async Task Eliminar_NotFound_YOk()
        {
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.SetupSequence(a => a.ObtenerAsync(5))
                .ReturnsAsync(new Respuesta<MotocicletaDTO> { Resultado = true, Dato = null! })
                .ReturnsAsync(new Respuesta<MotocicletaDTO> { Resultado = true, Dato = new MotocicletaDTO { Entry = 5 } });
            _app.Setup(a => a.EliminarAsync(5)).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().Eliminar(5)).Result);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Eliminar(5)).Result).Value);
        }
    }
}
