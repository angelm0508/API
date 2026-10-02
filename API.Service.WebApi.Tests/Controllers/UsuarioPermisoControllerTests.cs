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
    public class UsuarioPermisoControllerTests
    {
        private readonly Mock<IPermisoApplication> _applicationMock = new();
        private readonly UsuarioPermisoController _controller;

        public UsuarioPermisoControllerTests()
        {
            _controller = new UsuarioPermisoController(_applicationMock.Object)
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
        public async Task Obtener_Ok_DevuelveOk200()
        {
            _applicationMock.Setup(a => a.ObtenerMatrizUsuarioAsync(1))
                .ReturnsAsync(new Respuesta<IEnumerable<PermisoModuloDTO>> { Resultado = true, Dato = new List<PermisoModuloDTO>() });

            var resultado = await _controller.Obtener(1);

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Guardar_Ok_DevuelveOk200()
        {
            _applicationMock.Setup(a => a.GuardarMatrizUsuarioAsync(1, It.IsAny<List<PermisoModuloDTO>>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Guardar(1, new List<PermisoModuloDTO>());

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task AplicarPlantilla_Ok_DevuelveOk200()
        {
            _applicationMock.Setup(a => a.AplicarPlantillaAsync(1, 5))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.AplicarPlantilla(1, 5);

            Assert.IsType<OkObjectResult>(resultado.Result);
        }
    }
}
