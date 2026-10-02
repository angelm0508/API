using System.Security.Claims;
using API.Application.DTO;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using API.Application.DTO.factura;
using API.Application.DTO.facturaCompra;
using API.Application.DTO.entrega;
using API.Application.DTO.entregaCompra;
using API.Application.DTO.entradaMercancia;
using API.Application.DTO.salidaMercancia;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>
    /// Cancelar un documento (Cancelado == "S" en la actualizacion) exige Accion.Eliminar ademas de
    /// Actualizar. Cubre los seis controllers de documentos que ejecutan el reverso al cancelar.
    /// </summary>
    public class CancelarDocumentoPermisoTests
    {
        private static ControllerContext Contexto(params Claim[] claims) => new()
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };

        private static void AssertForbidden(ObjectResult? resultado)
        {
            Assert.NotNull(resultado);
            Assert.Equal(StatusCodes.Status403Forbidden, resultado!.StatusCode);
            var cuerpo = Assert.IsType<Respuesta<object>>(resultado.Value);
            Assert.Equal("No tenés permiso para realizar esta acción.", cuerpo.Mensaje);
        }

        [Fact]
        public void PermisoUsuario_Denegado_TieneLaMismaFormaQueElFiltro()
        {
            AssertForbidden(PermisoUsuario.Denegado());
        }

        // ---------- Factura (Facturas) ----------

        private (FacturaController Controller, Mock<IFacturaApplication> App) CrearFactura(params Claim[] claims)
        {
            var app = new Mock<IFacturaApplication>();
            app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<FacturaDTO> { Resultado = true, Dato = new FacturaDTO() });
            app.Setup(a => a.ActualizarAsync(1, It.IsAny<FacturaActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });
            return (new FacturaController(app.Object) { ControllerContext = Contexto(claims) }, app);
        }

        [Fact]
        public async Task Factura_Actualizar_CancelarSinEliminar_Devuelve403YNoActualiza()
        {
            var (controller, app) = CrearFactura(new Claim("SuperUsuario", "N"), new Claim("perm", "Facturas:VAU"));

            var resultado = await controller.ActualizarAsync(1, new FacturaActualizarDTO { Cancelado = "S" });

            AssertForbidden(resultado.Result as ObjectResult);
            app.Verify(a => a.ActualizarAsync(It.IsAny<int>(), It.IsAny<FacturaActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task Factura_Actualizar_CancelarConEliminar_Actualiza()
        {
            var (controller, app) = CrearFactura(new Claim("SuperUsuario", "N"), new Claim("perm", "Facturas:VAUE"));

            var resultado = await controller.ActualizarAsync(1, new FacturaActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
            app.Verify(a => a.ActualizarAsync(1, It.IsAny<FacturaActualizarDTO>()), Times.Once);
        }

        [Fact]
        public async Task Factura_Actualizar_CancelarSuperUsuario_Actualiza()
        {
            var (controller, _) = CrearFactura(new Claim("SuperUsuario", "S"));

            var resultado = await controller.ActualizarAsync(1, new FacturaActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Factura_Actualizar_SinCancelar_NoExigeEliminar()
        {
            var (controller, _) = CrearFactura(new Claim("SuperUsuario", "N"), new Claim("perm", "Facturas:VAU"));

            var resultado = await controller.ActualizarAsync(1, new FacturaActualizarDTO { Cancelado = "N" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        // ---------- FacturaCompra (FacturasCompra) ----------

        private (FacturaCompraController Controller, Mock<IFacturaCompraApplication> App) CrearFacturaCompra(params Claim[] claims)
        {
            var app = new Mock<IFacturaCompraApplication>();
            app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<FacturaCompraDTO> { Resultado = true, Dato = new FacturaCompraDTO() });
            app.Setup(a => a.ActualizarAsync(1, It.IsAny<FacturaCompraActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });
            return (new FacturaCompraController(app.Object) { ControllerContext = Contexto(claims) }, app);
        }

        [Fact]
        public async Task FacturaCompra_Actualizar_CancelarSinEliminar_Devuelve403YNoActualiza()
        {
            var (controller, app) = CrearFacturaCompra(new Claim("SuperUsuario", "N"), new Claim("perm", "FacturasCompra:VAU"));

            var resultado = await controller.ActualizarAsync(1, new FacturaCompraActualizarDTO { Cancelado = "S" });

            AssertForbidden(resultado.Result as ObjectResult);
            app.Verify(a => a.ActualizarAsync(It.IsAny<int>(), It.IsAny<FacturaCompraActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task FacturaCompra_Actualizar_CancelarConEliminar_Actualiza()
        {
            var (controller, app) = CrearFacturaCompra(new Claim("SuperUsuario", "N"), new Claim("perm", "FacturasCompra:VAUE"));

            var resultado = await controller.ActualizarAsync(1, new FacturaCompraActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
            app.Verify(a => a.ActualizarAsync(1, It.IsAny<FacturaCompraActualizarDTO>()), Times.Once);
        }

        [Fact]
        public async Task FacturaCompra_Actualizar_CancelarSuperUsuario_Actualiza()
        {
            var (controller, _) = CrearFacturaCompra(new Claim("SuperUsuario", "S"));

            var resultado = await controller.ActualizarAsync(1, new FacturaCompraActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task FacturaCompra_Actualizar_SinCancelar_NoExigeEliminar()
        {
            var (controller, _) = CrearFacturaCompra(new Claim("SuperUsuario", "N"), new Claim("perm", "FacturasCompra:VAU"));

            var resultado = await controller.ActualizarAsync(1, new FacturaCompraActualizarDTO { Cancelado = "N" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        // ---------- Entrega (Entregas) ----------

        private (EntregaController Controller, Mock<IEntregaApplication> App) CrearEntrega(params Claim[] claims)
        {
            var app = new Mock<IEntregaApplication>();
            app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<EntregaDTO> { Resultado = true, Dato = new EntregaDTO() });
            app.Setup(a => a.ActualizarAsync(1, It.IsAny<EntregaActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });
            return (new EntregaController(app.Object) { ControllerContext = Contexto(claims) }, app);
        }

        [Fact]
        public async Task Entrega_Actualizar_CancelarSinEliminar_Devuelve403YNoActualiza()
        {
            var (controller, app) = CrearEntrega(new Claim("SuperUsuario", "N"), new Claim("perm", "Entregas:VAU"));

            var resultado = await controller.ActualizarAsync(1, new EntregaActualizarDTO { Cancelado = "S" });

            AssertForbidden(resultado.Result as ObjectResult);
            app.Verify(a => a.ActualizarAsync(It.IsAny<int>(), It.IsAny<EntregaActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task Entrega_Actualizar_CancelarConEliminar_Actualiza()
        {
            var (controller, app) = CrearEntrega(new Claim("SuperUsuario", "N"), new Claim("perm", "Entregas:VAUE"));

            var resultado = await controller.ActualizarAsync(1, new EntregaActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
            app.Verify(a => a.ActualizarAsync(1, It.IsAny<EntregaActualizarDTO>()), Times.Once);
        }

        [Fact]
        public async Task Entrega_Actualizar_CancelarSuperUsuario_Actualiza()
        {
            var (controller, _) = CrearEntrega(new Claim("SuperUsuario", "S"));

            var resultado = await controller.ActualizarAsync(1, new EntregaActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Entrega_Actualizar_SinCancelar_NoExigeEliminar()
        {
            var (controller, _) = CrearEntrega(new Claim("SuperUsuario", "N"), new Claim("perm", "Entregas:VAU"));

            var resultado = await controller.ActualizarAsync(1, new EntregaActualizarDTO { Cancelado = "N" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        // ---------- EntregaCompra (EntregasCompra) ----------

        private (EntregaCompraController Controller, Mock<IEntregaCompraApplication> App) CrearEntregaCompra(params Claim[] claims)
        {
            var app = new Mock<IEntregaCompraApplication>();
            app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<EntregaCompraDTO> { Resultado = true, Dato = new EntregaCompraDTO() });
            app.Setup(a => a.ActualizarAsync(1, It.IsAny<EntregaCompraActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });
            return (new EntregaCompraController(app.Object) { ControllerContext = Contexto(claims) }, app);
        }

        [Fact]
        public async Task EntregaCompra_Actualizar_CancelarSinEliminar_Devuelve403YNoActualiza()
        {
            var (controller, app) = CrearEntregaCompra(new Claim("SuperUsuario", "N"), new Claim("perm", "EntregasCompra:VAU"));

            var resultado = await controller.ActualizarAsync(1, new EntregaCompraActualizarDTO { Cancelado = "S" });

            AssertForbidden(resultado.Result as ObjectResult);
            app.Verify(a => a.ActualizarAsync(It.IsAny<int>(), It.IsAny<EntregaCompraActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task EntregaCompra_Actualizar_CancelarConEliminar_Actualiza()
        {
            var (controller, app) = CrearEntregaCompra(new Claim("SuperUsuario", "N"), new Claim("perm", "EntregasCompra:VAUE"));

            var resultado = await controller.ActualizarAsync(1, new EntregaCompraActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
            app.Verify(a => a.ActualizarAsync(1, It.IsAny<EntregaCompraActualizarDTO>()), Times.Once);
        }

        [Fact]
        public async Task EntregaCompra_Actualizar_CancelarSuperUsuario_Actualiza()
        {
            var (controller, _) = CrearEntregaCompra(new Claim("SuperUsuario", "S"));

            var resultado = await controller.ActualizarAsync(1, new EntregaCompraActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task EntregaCompra_Actualizar_SinCancelar_NoExigeEliminar()
        {
            var (controller, _) = CrearEntregaCompra(new Claim("SuperUsuario", "N"), new Claim("perm", "EntregasCompra:VAU"));

            var resultado = await controller.ActualizarAsync(1, new EntregaCompraActualizarDTO { Cancelado = "N" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        // ---------- EntradaMercancia (EntradasMercancia) ----------

        private (EntradaMercanciaController Controller, Mock<IEntradaMercanciaApplication> App) CrearEntradaMercancia(params Claim[] claims)
        {
            var app = new Mock<IEntradaMercanciaApplication>();
            app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<EntradaMercanciaDTO> { Resultado = true, Dato = new EntradaMercanciaDTO() });
            app.Setup(a => a.ActualizarAsync(1, It.IsAny<EntradaMercanciaActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });
            return (new EntradaMercanciaController(app.Object) { ControllerContext = Contexto(claims) }, app);
        }

        [Fact]
        public async Task EntradaMercancia_Actualizar_CancelarSinEliminar_Devuelve403YNoActualiza()
        {
            var (controller, app) = CrearEntradaMercancia(new Claim("SuperUsuario", "N"), new Claim("perm", "EntradasMercancia:VAU"));

            var resultado = await controller.ActualizarAsync(1, new EntradaMercanciaActualizarDTO { Cancelado = "S" });

            AssertForbidden(resultado.Result as ObjectResult);
            app.Verify(a => a.ActualizarAsync(It.IsAny<int>(), It.IsAny<EntradaMercanciaActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task EntradaMercancia_Actualizar_CancelarConEliminar_Actualiza()
        {
            var (controller, app) = CrearEntradaMercancia(new Claim("SuperUsuario", "N"), new Claim("perm", "EntradasMercancia:VAUE"));

            var resultado = await controller.ActualizarAsync(1, new EntradaMercanciaActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
            app.Verify(a => a.ActualizarAsync(1, It.IsAny<EntradaMercanciaActualizarDTO>()), Times.Once);
        }

        [Fact]
        public async Task EntradaMercancia_Actualizar_CancelarSuperUsuario_Actualiza()
        {
            var (controller, _) = CrearEntradaMercancia(new Claim("SuperUsuario", "S"));

            var resultado = await controller.ActualizarAsync(1, new EntradaMercanciaActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task EntradaMercancia_Actualizar_SinCancelar_NoExigeEliminar()
        {
            var (controller, _) = CrearEntradaMercancia(new Claim("SuperUsuario", "N"), new Claim("perm", "EntradasMercancia:VAU"));

            var resultado = await controller.ActualizarAsync(1, new EntradaMercanciaActualizarDTO { Cancelado = "N" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        // ---------- SalidaMercancia (SalidasMercancia) ----------

        private (SalidaMercanciaController Controller, Mock<ISalidaMercanciaApplication> App) CrearSalidaMercancia(params Claim[] claims)
        {
            var app = new Mock<ISalidaMercanciaApplication>();
            app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<SalidaMercanciaDTO> { Resultado = true, Dato = new SalidaMercanciaDTO() });
            app.Setup(a => a.ActualizarAsync(1, It.IsAny<SalidaMercanciaActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });
            return (new SalidaMercanciaController(app.Object) { ControllerContext = Contexto(claims) }, app);
        }

        [Fact]
        public async Task SalidaMercancia_Actualizar_CancelarSinEliminar_Devuelve403YNoActualiza()
        {
            var (controller, app) = CrearSalidaMercancia(new Claim("SuperUsuario", "N"), new Claim("perm", "SalidasMercancia:VAU"));

            var resultado = await controller.ActualizarAsync(1, new SalidaMercanciaActualizarDTO { Cancelado = "S" });

            AssertForbidden(resultado.Result as ObjectResult);
            app.Verify(a => a.ActualizarAsync(It.IsAny<int>(), It.IsAny<SalidaMercanciaActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task SalidaMercancia_Actualizar_CancelarConEliminar_Actualiza()
        {
            var (controller, app) = CrearSalidaMercancia(new Claim("SuperUsuario", "N"), new Claim("perm", "SalidasMercancia:VAUE"));

            var resultado = await controller.ActualizarAsync(1, new SalidaMercanciaActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
            app.Verify(a => a.ActualizarAsync(1, It.IsAny<SalidaMercanciaActualizarDTO>()), Times.Once);
        }

        [Fact]
        public async Task SalidaMercancia_Actualizar_CancelarSuperUsuario_Actualiza()
        {
            var (controller, _) = CrearSalidaMercancia(new Claim("SuperUsuario", "S"));

            var resultado = await controller.ActualizarAsync(1, new SalidaMercanciaActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task SalidaMercancia_Actualizar_SinCancelar_NoExigeEliminar()
        {
            var (controller, _) = CrearSalidaMercancia(new Claim("SuperUsuario", "N"), new Claim("perm", "SalidasMercancia:VAU"));

            var resultado = await controller.ActualizarAsync(1, new SalidaMercanciaActualizarDTO { Cancelado = "N" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }
    }
}
