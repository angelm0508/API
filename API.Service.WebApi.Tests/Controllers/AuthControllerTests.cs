using API.Application.DTO;
using API.Application.DTO.autenticacion;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using API.Transversal.Common;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthApplication> _applicationMock;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _applicationMock = new Mock<IAuthApplication>();
            _controller = new AuthController(_applicationMock.Object);
        }

        [Fact]
        public async Task Login_DevuelveBadRequest_CuandoCredencialesInvalidas()
        {
            var loginDto = new LoginDTO { Usuario = "user1", Contrasena = "wrong" };
            _applicationMock.Setup(a => a.LoginAsync(loginDto))
                .ReturnsAsync(new Respuesta<LoginResponseDTO> { Resultado = false, Mensaje = "Credenciales inválidas." });

            var resultado = await _controller.Login(loginDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.IsType<RespuestaError>(badRequest.Value);
        }

        [Fact]
        public async Task Login_DevuelveOk_CuandoCredencialesValidas()
        {
            var loginDto = new LoginDTO { Usuario = "user1", Contrasena = "correct" };
            var loginResponse = new LoginResponseDTO { Resultado = true, Token = "token123", UsuarioNombre = "user1" };
            _applicationMock.Setup(a => a.LoginAsync(loginDto))
                .ReturnsAsync(new Respuesta<LoginResponseDTO> { Resultado = true, Dato = loginResponse });

            var resultado = await _controller.Login(loginDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(loginResponse, ok.Value);
        }
    }
}
