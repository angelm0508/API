using System.Security.Claims;
using API.Application.DTO;
using API.Application.DTO.permiso;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class PlantillaControllerTests
    {
        private readonly Mock<IPlantillaApplication> _applicationMock = new();
        private readonly Mock<IPermisoApplication> _permisoApplicationMock = new();
        private readonly PlantillaController _controller;

        public PlantillaControllerTests()
        {
            _controller = new PlantillaController(_applicationMock.Object, _permisoApplicationMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("SuperUsuario", "S") }, "Test"))
                    }
                }
            };
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<PlantillaDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Obtener(1);

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNull()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<PlantillaDTO> { Resultado = true, Dato = null });

            var resultado = await _controller.Obtener(1);

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Permisos_Ok_DevuelveOk200()
        {
            _permisoApplicationMock.Setup(a => a.ObtenerMatrizPlantillaAsync(5))
                .ReturnsAsync(new Respuesta<IEnumerable<PermisoModuloDTO>> { Resultado = true, Dato = new List<PermisoModuloDTO>() });

            var resultado = await _controller.Permisos(5);

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task GuardarPermisos_Ok_DevuelveOk200()
        {
            _permisoApplicationMock.Setup(a => a.GuardarMatrizPlantillaAsync(5, It.IsAny<List<PermisoModuloDTO>>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.GuardarPermisos(5, new List<PermisoModuloDTO>());

            Assert.IsType<OkObjectResult>(resultado.Result);
        }
    }
}
