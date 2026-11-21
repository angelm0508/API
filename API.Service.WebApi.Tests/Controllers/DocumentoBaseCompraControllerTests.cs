using System.Reflection;
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
    public class DocumentoBaseCompraControllerTests
    {
        private static ControllerContext Contexto(params Claim[] claims) => new()
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };

        private (DocumentoBaseCompraController Controller, Mock<IDocumentoBaseCompraApplication> App) Crear(params Claim[] claims)
        {
            var app = new Mock<IDocumentoBaseCompraApplication>();
            return (new DocumentoBaseCompraController(app.Object) { ControllerContext = Contexto(claims) }, app);
        }

        private static Claim[] Ambos(string a, string b) => new[] { new Claim("SuperUsuario", "N"), new Claim("perm", a), new Claim("perm", b) };

        [Fact]
        public async Task Abierto_ConPermisos_DevuelveOk()
        {
            var (c, app) = Crear(Ambos("PedidosCompra:V", "EntregasCompra:A"));
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO> { Resultado = true, Dato = new DocumentoBaseBorradorDTO() };
            app.Setup(a => a.ObtenerBorradorAsync(11, 10, 12)).ReturnsAsync(respuesta);

            var r = await c.ObtenerAbiertoAsync(11, 10, 12);

            Assert.Same(respuesta, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Abierto_DeEntregaAFactura_ConPermisos_DevuelveOk()
        {
            var (c, app) = Crear(Ambos("EntregasCompra:V", "FacturasCompra:A"));
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO> { Resultado = true, Dato = new DocumentoBaseBorradorDTO() };
            app.Setup(a => a.ObtenerBorradorAsync(12, 3, 13)).ReturnsAsync(respuesta);

            var r = await c.ObtenerAbiertoAsync(12, 3, 13);

            Assert.Same(respuesta, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Abierto_SinVerEnElOrigen_Devuelve403()
        {
            var (c, app) = Crear(Ambos("PedidosCompra:A", "EntregasCompra:A"));

            var r = await c.ObtenerAbiertoAsync(11, 10, 12);

            Assert.Equal(403, Assert.IsType<ObjectResult>(r.Result).StatusCode);
            app.Verify(a => a.ObtenerBorradorAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Abierto_SinAgregarEnElDestino_Devuelve403()
        {
            var (c, app) = Crear(Ambos("PedidosCompra:V", "EntregasCompra:V"));

            var r = await c.ObtenerAbiertoAsync(11, 10, 12);

            Assert.Equal(403, Assert.IsType<ObjectResult>(r.Result).StatusCode);
            app.Verify(a => a.ObtenerBorradorAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Abierto_SuperUsuario_Pasa()
        {
            var (c, app) = Crear(new Claim("SuperUsuario", "S"));
            app.Setup(a => a.ObtenerBorradorAsync(11, 1, 13)).ReturnsAsync(new Respuesta<DocumentoBaseBorradorDTO> { Resultado = true });

            var r = await c.ObtenerAbiertoAsync(11, 1, 13);

            Assert.IsType<OkObjectResult>(r.Result);
        }

        [Theory]
        [InlineData(4, 12)]
        [InlineData(11, 5)]
        [InlineData(3, 4)]
        public async Task Abierto_TipoInvalido_Devuelve400(int origen, int destino)
        {
            var (c, app) = Crear(new Claim("SuperUsuario", "S"));

            var r = await c.ObtenerAbiertoAsync(origen, 1, destino);

            var bad = Assert.IsType<BadRequestObjectResult>(r.Result);
            Assert.Equal("El tipo de documento no es válido.", Assert.IsType<Respuesta<DocumentoBaseBorradorDTO>>(bad.Value).Mensaje);
            app.Verify(a => a.ObtenerBorradorAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Abierto_OrigenInexistente_Devuelve404()
        {
            var (c, app) = Crear(new Claim("SuperUsuario", "S"));
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO> { Resultado = false, Mensaje = DocumentoBaseMensajes.OrigenNoExiste };
            app.Setup(a => a.ObtenerBorradorAsync(11, 99, 12)).ReturnsAsync(respuesta);

            var r = await c.ObtenerAbiertoAsync(11, 99, 12);

            Assert.Same(respuesta, Assert.IsType<NotFoundObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Abierto_OtroError_Devuelve400ConElMensaje()
        {
            var (c, app) = Crear(new Claim("SuperUsuario", "S"));
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO> { Resultado = false, Mensaje = "El documento no tiene líneas abiertas." };
            app.Setup(a => a.ObtenerBorradorAsync(11, 1, 12)).ReturnsAsync(respuesta);

            var r = await c.ObtenerAbiertoAsync(11, 1, 12);

            Assert.Same(respuesta, Assert.IsType<BadRequestObjectResult>(r.Result).Value);
        }

        [Fact]
        public void Rutas_SonLasDeComprasNoLasDeVentas()
        {
            var ruta = typeof(DocumentoBaseCompraController).GetCustomAttribute<RouteAttribute>();
            Assert.Equal("api/DocumentoBaseCompra", ruta?.Template);

            var accion = typeof(DocumentoBaseCompraController).GetMethod(nameof(DocumentoBaseCompraController.ObtenerAbiertoAsync));
            var get = accion?.GetCustomAttribute<HttpGetAttribute>();
            Assert.Equal("{tipoOrigen:int}/{entry:int}/Abierto", get?.Template);
        }
    }
}
