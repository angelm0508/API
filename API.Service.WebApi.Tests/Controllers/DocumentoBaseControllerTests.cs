using System.Security.Claims;
using API.Application.DTO;
using API.Application.DTO.documentoBase;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class DocumentoBaseControllerTests
    {
        private static ControllerContext Contexto(params Claim[] claims) => new()
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };

        private (DocumentoBaseController Controller, Mock<IDocumentoBaseApplication> App) Crear(params Claim[] claims)
        {
            var app = new Mock<IDocumentoBaseApplication>();
            return (new DocumentoBaseController(app.Object) { ControllerContext = Contexto(claims) }, app);
        }

        private static Claim[] Permisos(string perm) => new[] { new Claim("SuperUsuario", "N"), new Claim("perm", perm) };

        private static Claim[] Ambos(string a, string b) => new[] { new Claim("SuperUsuario", "N"), new Claim("perm", a), new Claim("perm", b) };

        [Fact]
        public async Task Abierto_ConPermisos_DevuelveOk()
        {
            var (c, app) = Crear(Ambos("Cotizaciones:V", "Pedidos:A"));
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO> { Resultado = true, Dato = new DocumentoBaseBorradorDTO() };
            app.Setup(a => a.ObtenerBorradorAsync(3, 10, 4)).ReturnsAsync(respuesta);

            var r = await c.ObtenerAbiertoAsync(3, 10, 4);

            Assert.Same(respuesta, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Abierto_SinVerEnElOrigen_Devuelve403()
        {
            var (c, app) = Crear(Ambos("Cotizaciones:A", "Pedidos:A"));

            var r = await c.ObtenerAbiertoAsync(3, 10, 4);

            Assert.Equal(403, Assert.IsType<ObjectResult>(r.Result).StatusCode);
            app.Verify(a => a.ObtenerBorradorAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Abierto_SinAgregarEnElDestino_Devuelve403()
        {
            var (c, app) = Crear(Ambos("Cotizaciones:V", "Pedidos:V"));

            var r = await c.ObtenerAbiertoAsync(3, 10, 4);

            Assert.Equal(403, Assert.IsType<ObjectResult>(r.Result).StatusCode);
            app.Verify(a => a.ObtenerBorradorAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Abierto_SuperUsuario_Pasa()
        {
            var (c, app) = Crear(new Claim("SuperUsuario", "S"));
            app.Setup(a => a.ObtenerBorradorAsync(5, 1, 6)).ReturnsAsync(new Respuesta<DocumentoBaseBorradorDTO> { Resultado = true });

            var r = await c.ObtenerAbiertoAsync(5, 1, 6);

            Assert.IsType<OkObjectResult>(r.Result);
        }

        [Theory]
        [InlineData(9, 4)]
        [InlineData(3, 9)]
        public async Task Abierto_TipoDesconocido_Devuelve400(int origen, int destino)
        {
            var (c, app) = Crear(new Claim("SuperUsuario", "S"));

            var r = await c.ObtenerAbiertoAsync(origen, 1, destino);

            Assert.IsType<BadRequestObjectResult>(r.Result);
            app.Verify(a => a.ObtenerBorradorAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Abierto_OrigenInexistente_Devuelve404()
        {
            var (c, app) = Crear(new Claim("SuperUsuario", "S"));
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO> { Resultado = false, Mensaje = DocumentoBaseMensajes.OrigenNoExiste };
            app.Setup(a => a.ObtenerBorradorAsync(3, 99, 4)).ReturnsAsync(respuesta);

            var r = await c.ObtenerAbiertoAsync(3, 99, 4);

            Assert.Same(respuesta, Assert.IsType<NotFoundObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Abierto_OtroError_Devuelve400ConElMensaje()
        {
            var (c, app) = Crear(new Claim("SuperUsuario", "S"));
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO> { Resultado = false, Mensaje = "El documento no tiene líneas abiertas." };
            app.Setup(a => a.ObtenerBorradorAsync(3, 1, 4)).ReturnsAsync(respuesta);

            var r = await c.ObtenerAbiertoAsync(3, 1, 4);

            Assert.Same(respuesta, Assert.IsType<BadRequestObjectResult>(r.Result).Value);
        }
    }
}
