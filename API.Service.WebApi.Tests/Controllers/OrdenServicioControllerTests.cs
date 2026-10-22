using API.Application.DTO;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class OrdenServicioControllerTests
    {
        private readonly Mock<IOrdenServicioApplication> _app = new();

        private OrdenServicioController Crear() => new(_app.Object);

        private void Existe(int id) =>
            _app.Setup(a => a.ObtenerAsync(id)).ReturnsAsync(new Respuesta<OrdenServicioDTO> { Resultado = true, Dato = new OrdenServicioDTO { Entry = id } });

        private void NoExiste(int id) =>
            _app.Setup(a => a.ObtenerAsync(id)).ReturnsAsync(new Respuesta<OrdenServicioDTO> { Resultado = true, Dato = null! });

        [Fact]
        public async Task Obtener_BadRequest_NotFound_YOk()
        {
            var mala = new Respuesta<OrdenServicioDTO> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<OrdenServicioDTO> { Resultado = true, Dato = new OrdenServicioDTO { Entry = 1 } };
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(mala);
            _app.Setup(a => a.ObtenerAsync(2)).ReturnsAsync(buena);
            NoExiste(3);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Obtener(1)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Obtener(2)).Result).Value);
            var notFound = Assert.IsType<NotFoundObjectResult>((await Crear().Obtener(3)).Result);
            Assert.Equal("La orden de servicio no se encontró.", Assert.IsType<Respuesta<OrdenServicioDTO>>(notFound.Value).Mensaje);
        }

        [Fact]
        public async Task ObtenerTodo_PasaElFiltroDeEstado_YDevuelveBadRequestOOk()
        {
            var mala = new Respuesta<IEnumerable<OrdenServicioDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<OrdenServicioDTO>> { Resultado = true, Dato = new[] { new OrdenServicioDTO() } };
            _app.Setup(a => a.ObtenerTodoAsync("T")).ReturnsAsync(buena);
            _app.Setup(a => a.ObtenerTodoAsync(null)).ReturnsAsync(mala);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodoAsync("T")).Result).Value);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodoAsync(null)).Result).Value);
        }

        [Fact]
        public async Task ObtenerLineas_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<OrdenServicioDetalleDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<OrdenServicioDetalleDTO>> { Resultado = true, Dato = new[] { new OrdenServicioDetalleDTO() } };
            _app.SetupSequence(a => a.ObtenerLineasAsync(5)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerLineas(5)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerLineas(5)).Result).Value);
        }

        [Fact]
        public async Task Insertar_BadRequestYOk()
        {
            var mala = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<int> { Resultado = true, Dato = 42 };
            var dto = new OrdenServicioCrearDTO { Serie = 3 };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().InsertarAsync(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().InsertarAsync(dto)).Result).Value);
        }

        [Fact]
        public async Task Actualizar_NotFound_BadRequest_YOk()
        {
            var dto = new OrdenServicioActualizarDTO { MotoEntry = 10 };
            NoExiste(1);
            Existe(2);
            Existe(3);
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.ActualizarAsync(2, dto)).ReturnsAsync(mala);
            _app.Setup(a => a.ActualizarAsync(3, dto)).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().ActualizarAsync(1, dto)).Result);
            _app.Verify(a => a.ActualizarAsync(1, It.IsAny<OrdenServicioActualizarDTO>()), Times.Never);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ActualizarAsync(2, dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ActualizarAsync(3, dto)).Result).Value);
        }

        [Fact]
        public async Task CambiarEstado_NotFound_BadRequest_YOk()
        {
            NoExiste(1);
            Existe(2);
            Existe(3);
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "No se puede pasar de Terminada a En proceso: la orden solo avanza." };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.CambiarEstadoAsync(2, "P")).ReturnsAsync(mala);
            _app.Setup(a => a.CambiarEstadoAsync(3, "T")).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().CambiarEstadoAsync(1, new OrdenServicioEstadoDTO { Estado = "P" })).Result);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().CambiarEstadoAsync(2, new OrdenServicioEstadoDTO { Estado = "P" })).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().CambiarEstadoAsync(3, new OrdenServicioEstadoDTO { Estado = "T" })).Result).Value);
        }

        [Fact]
        public async Task Cancelar_NotFound_BadRequest_YOk()
        {
            NoExiste(1);
            Existe(2);
            Existe(3);
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos." };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.CancelarAsync(2)).ReturnsAsync(mala);
            _app.Setup(a => a.CancelarAsync(3)).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().CancelarAsync(1)).Result);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().CancelarAsync(2)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().CancelarAsync(3)).Result).Value);
        }
    }
}
