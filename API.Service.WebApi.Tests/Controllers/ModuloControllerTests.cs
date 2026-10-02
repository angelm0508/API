using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class ModuloControllerTests
    {
        private readonly Mock<IPermisoApplication> _applicationMock = new();
        private readonly ModuloController _controller;

        public ModuloControllerTests()
        {
            _controller = new ModuloController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_Ok_DevuelveOk200()
        {
            _applicationMock.Setup(a => a.ObtenerModulosAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<ModuloDTO>> { Resultado = true, Dato = new List<ModuloDTO>() });

            var resultado = await _controller.Obtener();

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_Falla_DevuelveBadRequest()
        {
            _applicationMock.Setup(a => a.ObtenerModulosAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<ModuloDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Obtener();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }
    }
}
