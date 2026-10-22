using API.Application.DTO;
using API.Application.DTO.mecanico;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class MecanicoControllerTests
    {
        private readonly Mock<IMecanicoApplication> _app = new();

        private MecanicoController Crear() => new(_app.Object);

        [Fact]
        public async Task ObtenerTodo_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<MecanicoDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<MecanicoDTO>> { Resultado = true, Dato = new[] { new MecanicoDTO() } };
            _app.SetupSequence(a => a.ObtenerAsync()).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodo()).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodo()).Result).Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerPorCodigoAsync("M99")).ReturnsAsync(new Respuesta<MecanicoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().ObtenerPorCodigo("M99");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Equal("Código de mecánico no encontrado.", Assert.IsType<Respuesta<MecanicoDTO>>(notFound.Value).Mensaje);
        }

        [Fact]
        public async Task Crear_BadRequestYOk()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            var dto = new MecanicoCrearDTO { Codigo = "M01", Nombre = "Juan" };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Crear(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Crear(dto)).Result).Value);
        }

        [Fact]
        public async Task Actualizar_NotFound_CuandoNoExiste()
        {
            _app.Setup(a => a.ObtenerPorCodigoAsync("M99")).ReturnsAsync(new Respuesta<MecanicoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Actualizar("M99", new MecanicoActualizarDTO { Nombre = "X" });

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
            _app.Verify(a => a.ActualizarAsync(It.IsAny<string>(), It.IsAny<MecanicoActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_Ok_CuandoExiste()
        {
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            var dto = new MecanicoActualizarDTO { Nombre = "Juan P." };
            _app.Setup(a => a.ObtenerPorCodigoAsync("M01")).ReturnsAsync(new Respuesta<MecanicoDTO> { Resultado = true, Dato = new MecanicoDTO { Codigo = "M01" } });
            _app.Setup(a => a.ActualizarAsync("M01", dto)).ReturnsAsync(buena);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Actualizar("M01", dto)).Result).Value);
        }

        [Fact]
        public async Task Eliminar_NotFoundYOk()
        {
            _app.SetupSequence(a => a.ObtenerPorCodigoAsync("M01"))
                .ReturnsAsync(new Respuesta<MecanicoDTO> { Resultado = true, Dato = null! })
                .ReturnsAsync(new Respuesta<MecanicoDTO> { Resultado = true, Dato = new MecanicoDTO { Codigo = "M01" } });
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.EliminarAsync("M01")).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().Eliminar("M01")).Result);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Eliminar("M01")).Result).Value);
        }
    }
}
