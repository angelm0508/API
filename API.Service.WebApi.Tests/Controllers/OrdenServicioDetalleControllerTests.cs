using API.Application.DTO;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class OrdenServicioDetalleControllerTests
    {
        private readonly Mock<IOrdenServicioDetalleApplication> _app = new();

        private OrdenServicioDetalleController Crear() => new(_app.Object);

        [Fact]
        public async Task Insertar_BadRequestYOk()
        {
            var mala = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<int> { Resultado = true, Dato = 3 };
            var dto = new OrdenServicioDetalleCrearDTO { Entry = 50, CodArticulo = "ACE-1" };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().InsertarAsync(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().InsertarAsync(dto)).Result).Value);
        }

        [Fact]
        public async Task Actualizar_BadRequestYOk()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            var dto = new OrdenServicioDetalleActualizarDTO { Cantidad = 2m };
            _app.SetupSequence(a => a.ActualizarAsync(50, 1, dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ActualizarAsync(50, 1, dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ActualizarAsync(50, 1, dto)).Result).Value);
        }

        [Fact]
        public async Task Eliminar_BadRequestYOk()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.SetupSequence(a => a.EliminarAsync(50, 1)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().EliminarAsync(50, 1)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().EliminarAsync(50, 1)).Result).Value);
        }
    }
}
