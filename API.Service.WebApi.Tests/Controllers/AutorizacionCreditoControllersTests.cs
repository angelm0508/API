using System.Security.Claims;
using System.Text.Json;
using API.Application.DTO;
using API.Application.DTO.creditoCliente;
using API.Application.DTO.entrega;
using API.Application.DTO.factura;
using API.Application.DTO.pedido;
using API.Application.Interface;
using API.Domain.Core;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>
    /// Autorización de excesos de crédito (403 sin permiso, contexto por petición con permiso), permiso de pago inmediato
    /// y respuesta 409 con la posición de crédito, en las acciones de creación de Pedido, Entrega y Factura.
    /// </summary>
    public class AutorizacionCreditoControllersTests
    {
        private const string MensajeSinAutorizar = "No tenés permiso para autorizar excesos de crédito.";
        private const string MensajeSinPagos = "No tenés permiso para registrar pagos.";

        private static ControllerContext Contexto(params Claim[] claims) => new()
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };

        private static Claim[] Usuario(string permisos = "", string nombre = "jperez", string superUsuario = "N")
        {
            var claims = new List<Claim> { new(ClaimTypes.Name, nombre), new("SuperUsuario", superUsuario) };
            foreach (var p in permisos.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                claims.Add(new Claim("perm", p));
            return claims.ToArray();
        }

        private static void AssertForbidden(ObjectResult? resultado, string mensaje)
        {
            Assert.NotNull(resultado);
            Assert.Equal(StatusCodes.Status403Forbidden, resultado!.StatusCode);
            var cuerpo = Assert.IsType<Respuesta<object>>(resultado.Value);
            Assert.False(cuerpo.Resultado);
            Assert.Equal(mensaje, cuerpo.Mensaje);
        }

        private static RespuestaCredito<T> ExcesoCredito<T>() => new()
        {
            Resultado = false,
            Mensaje = "El cliente C001 excede su límite de crédito: límite 5,000.00, exposición 4,500.00, documento 800.00, exceso 300.00.",
            Credito = new CreditoExcedidoDTO
            {
                Codigo = "LIMITE_CREDITO_EXCEDIDO", Limite = 5000m, Saldo = 3000m, Comprometido = 1500m,
                Vencido = 0m, Monto = 800m, Exceso = 300m, Disponible = 500m
            }
        };

        private static async Task<JsonElement> SerializarComoLaApiAsync(IActionResult resultado)
        {
            // Mismo camino que la API: AddControllers() con el formateador JSON por defecto (camelCase) ejecutando el ObjectResult.
            var servicios = new ServiceCollection();
            servicios.AddLogging();
            servicios.AddControllers();
            using var proveedor = servicios.BuildServiceProvider();
            var http = new DefaultHttpContext { RequestServices = proveedor };
            http.Response.Body = new MemoryStream();

            await resultado.ExecuteResultAsync(new ActionContext(http, new RouteData(), new ActionDescriptor()));

            http.Response.Body.Position = 0;
            using var doc = await JsonDocument.ParseAsync(http.Response.Body);
            return doc.RootElement.Clone();
        }

        // ---------- Pedido: alta ----------

        private (PedidoController Controller, Mock<IPedidoApplication> App, AutorizacionCreditoContexto Ctx) CrearPedido(params Claim[] claims)
        {
            var app = new Mock<IPedidoApplication>();
            app.Setup(a => a.InsertarAsync(It.IsAny<PedidoCrearDTO>())).ReturnsAsync(new Respuesta<int> { Resultado = true, Dato = 7 });
            app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<PedidoDTO> { Resultado = true, Dato = new PedidoDTO() });
            app.Setup(a => a.ActualizarAsync(1, It.IsAny<PedidoActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });
            var ctx = new AutorizacionCreditoContexto();
            return (new PedidoController(app.Object, ctx) { ControllerContext = Contexto(claims) }, app, ctx);
        }

        [Fact]
        public async Task Pedido_Insertar_PideAutorizarSinPermiso_403YNoLlamaALaAplicacion()
        {
            var (controller, app, ctx) = CrearPedido(Usuario("Pedidos:VA"));

            var resultado = await controller.InsertarAsync(new PedidoCrearDTO { AutorizarExcesoCredito = true });

            AssertForbidden(resultado.Result as ObjectResult, MensajeSinAutorizar);
            app.Verify(a => a.InsertarAsync(It.IsAny<PedidoCrearDTO>()), Times.Never);
            Assert.False(ctx.Autorizada);
            Assert.Null(ctx.Usuario);
        }

        [Fact]
        public async Task Pedido_Insertar_PideAutorizarConPermiso_LlenaElContextoYLlamaALaAplicacion()
        {
            var (controller, app, ctx) = CrearPedido(Usuario("Pedidos:VA AutorizarExcesoCredito:V", nombre: "mgarcia"));

            var resultado = await controller.InsertarAsync(new PedidoCrearDTO { AutorizarExcesoCredito = true });

            Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.True(ctx.Autorizada);
            Assert.Equal("mgarcia", ctx.Usuario);
            app.Verify(a => a.InsertarAsync(It.IsAny<PedidoCrearDTO>()), Times.Once);
        }

        [Fact]
        public async Task Pedido_Insertar_SuperUsuarioPuedeAutorizar()
        {
            var (controller, _, ctx) = CrearPedido(Usuario(superUsuario: "S", nombre: "admin"));

            await controller.InsertarAsync(new PedidoCrearDTO { AutorizarExcesoCredito = true });

            Assert.True(ctx.Autorizada);
            Assert.Equal("admin", ctx.Usuario);
        }

        [Fact]
        public async Task Pedido_Insertar_SinPedirAutorizar_NoTocaElContextoAunqueTengaElPermiso()
        {
            var (controller, app, ctx) = CrearPedido(Usuario("Pedidos:VA AutorizarExcesoCredito:V"));

            var resultado = await controller.InsertarAsync(new PedidoCrearDTO { AutorizarExcesoCredito = false });

            Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.False(ctx.Autorizada);
            Assert.Null(ctx.Usuario);
            app.Verify(a => a.InsertarAsync(It.IsAny<PedidoCrearDTO>()), Times.Once);
        }

        [Fact]
        public async Task Pedido_Insertar_ExcesoDeCredito_Devuelve409ConElCuerpo()
        {
            var (controller, app, _) = CrearPedido(Usuario("Pedidos:VA"));
            var exceso = ExcesoCredito<int>();
            app.Setup(a => a.InsertarAsync(It.IsAny<PedidoCrearDTO>())).ReturnsAsync(exceso);

            var resultado = await controller.InsertarAsync(new PedidoCrearDTO());

            var conflicto = Assert.IsType<ConflictObjectResult>(resultado.Result);
            Assert.Equal(StatusCodes.Status409Conflict, conflicto.StatusCode);
            Assert.Same(exceso, conflicto.Value);
        }

        [Fact]
        public async Task Pedido_Insertar_ErrorSinCredito_SigueSiendoBadRequest()
        {
            var (controller, app, _) = CrearPedido(Usuario("Pedidos:VA"));
            app.Setup(a => a.InsertarAsync(It.IsAny<PedidoCrearDTO>())).ReturnsAsync(new Respuesta<int> { Resultado = false, Mensaje = "otro" });
            var resultado = await controller.InsertarAsync(new PedidoCrearDTO());
            Assert.IsType<BadRequestObjectResult>(resultado.Result);

            // Una RespuestaCredito sin la posición (Credito nulo) tampoco es un 409.
            app.Setup(a => a.InsertarAsync(It.IsAny<PedidoCrearDTO>())).ReturnsAsync(new RespuestaCredito<int> { Resultado = false, Mensaje = "otro" });
            resultado = await controller.InsertarAsync(new PedidoCrearDTO());
            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Pedido_Insertar_El409SerializaLaPropiedadCredito()
        {
            var (controller, app, _) = CrearPedido(Usuario("Pedidos:VA"));
            app.Setup(a => a.InsertarAsync(It.IsAny<PedidoCrearDTO>())).ReturnsAsync(ExcesoCredito<int>());

            var resultado = await controller.InsertarAsync(new PedidoCrearDTO());

            var conflicto = Assert.IsType<ConflictObjectResult>(resultado.Result);
            var json = await SerializarComoLaApiAsync(conflicto);
            Assert.False(json.GetProperty("resultado").GetBoolean());
            Assert.StartsWith("El cliente C001 excede su límite", json.GetProperty("mensaje").GetString());
            Assert.True(json.TryGetProperty("dato", out _));
            var credito = json.GetProperty("credito");
            Assert.Equal("LIMITE_CREDITO_EXCEDIDO", credito.GetProperty("codigo").GetString());
            Assert.Equal(5000m, credito.GetProperty("limite").GetDecimal());
            Assert.Equal(3000m, credito.GetProperty("saldo").GetDecimal());
            Assert.Equal(1500m, credito.GetProperty("comprometido").GetDecimal());
            Assert.Equal(0m, credito.GetProperty("vencido").GetDecimal());
            Assert.Equal(800m, credito.GetProperty("monto").GetDecimal());
            Assert.Equal(300m, credito.GetProperty("exceso").GetDecimal());
            Assert.Equal(500m, credito.GetProperty("disponible").GetDecimal());
        }

        // ---------- Pedido: PUT (reapertura) ----------

        [Fact]
        public async Task Pedido_Actualizar_PideAutorizarSinPermiso_403YNoLlamaALaAplicacion()
        {
            var (controller, app, ctx) = CrearPedido(Usuario("Pedidos:VU"));

            var resultado = await controller.ActualizarAsync(1, new PedidoActualizarDTO { AutorizarExcesoCredito = true });

            AssertForbidden(resultado.Result as ObjectResult, MensajeSinAutorizar);
            app.Verify(a => a.ActualizarAsync(It.IsAny<int>(), It.IsAny<PedidoActualizarDTO>()), Times.Never);
            Assert.False(ctx.Autorizada);
        }

        [Fact]
        public async Task Pedido_Actualizar_PideAutorizarConPermiso_LlenaElContexto()
        {
            var (controller, app, ctx) = CrearPedido(Usuario("Pedidos:VU AutorizarExcesoCredito:V", nombre: "mgarcia"));

            var resultado = await controller.ActualizarAsync(1, new PedidoActualizarDTO { AutorizarExcesoCredito = true });

            Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.True(ctx.Autorizada);
            Assert.Equal("mgarcia", ctx.Usuario);
            app.Verify(a => a.ActualizarAsync(1, It.IsAny<PedidoActualizarDTO>()), Times.Once);
        }

        [Fact]
        public async Task Pedido_Actualizar_SinPedirAutorizar_NoTocaElContexto()
        {
            var (controller, _, ctx) = CrearPedido(Usuario("Pedidos:VU AutorizarExcesoCredito:V"));

            var resultado = await controller.ActualizarAsync(1, new PedidoActualizarDTO());

            Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.False(ctx.Autorizada);
            Assert.Null(ctx.Usuario);
        }

        [Fact]
        public async Task Pedido_Actualizar_ExcesoAlReabrir_Devuelve409()
        {
            var (controller, app, _) = CrearPedido(Usuario("Pedidos:VU"));
            var exceso = ExcesoCredito<bool>();
            app.Setup(a => a.ActualizarAsync(1, It.IsAny<PedidoActualizarDTO>())).ReturnsAsync(exceso);

            var resultado = await controller.ActualizarAsync(1, new PedidoActualizarDTO());

            Assert.Same(exceso, Assert.IsType<ConflictObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task Pedido_Actualizar_ErrorSinCredito_SigueSiendoBadRequest()
        {
            var (controller, app, _) = CrearPedido(Usuario("Pedidos:VU"));
            app.Setup(a => a.ActualizarAsync(1, It.IsAny<PedidoActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "otro" });

            var resultado = await controller.ActualizarAsync(1, new PedidoActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        // ---------- Entrega: alta ----------

        private (EntregaController Controller, Mock<IEntregaApplication> App, AutorizacionCreditoContexto Ctx) CrearEntrega(params Claim[] claims)
        {
            var app = new Mock<IEntregaApplication>();
            app.Setup(a => a.InsertarAsync(It.IsAny<EntregaCrearDTO>())).ReturnsAsync(new Respuesta<int> { Resultado = true, Dato = 3 });
            var ctx = new AutorizacionCreditoContexto();
            return (new EntregaController(app.Object, ctx) { ControllerContext = Contexto(claims) }, app, ctx);
        }

        [Fact]
        public async Task Entrega_Insertar_PideAutorizarSinPermiso_403YNoLlamaALaAplicacion()
        {
            var (controller, app, ctx) = CrearEntrega(Usuario("Entregas:VA"));

            var resultado = await controller.InsertarAsync(new EntregaCrearDTO { AutorizarExcesoCredito = true });

            AssertForbidden(resultado.Result as ObjectResult, MensajeSinAutorizar);
            app.Verify(a => a.InsertarAsync(It.IsAny<EntregaCrearDTO>()), Times.Never);
            Assert.False(ctx.Autorizada);
        }

        [Fact]
        public async Task Entrega_Insertar_PideAutorizarConPermiso_LlenaElContexto()
        {
            var (controller, app, ctx) = CrearEntrega(Usuario("Entregas:VA AutorizarExcesoCredito:V", nombre: "mgarcia"));

            var resultado = await controller.InsertarAsync(new EntregaCrearDTO { AutorizarExcesoCredito = true });

            Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.True(ctx.Autorizada);
            Assert.Equal("mgarcia", ctx.Usuario);
            app.Verify(a => a.InsertarAsync(It.IsAny<EntregaCrearDTO>()), Times.Once);
        }

        [Fact]
        public async Task Entrega_Insertar_SinPedirAutorizar_NoTocaElContexto()
        {
            var (controller, _, ctx) = CrearEntrega(Usuario("Entregas:VA AutorizarExcesoCredito:V"));

            await controller.InsertarAsync(new EntregaCrearDTO());

            Assert.False(ctx.Autorizada);
            Assert.Null(ctx.Usuario);
        }

        [Fact]
        public async Task Entrega_Insertar_Exceso_Devuelve409YOtroErrorBadRequest()
        {
            var (controller, app, _) = CrearEntrega(Usuario("Entregas:VA"));
            var exceso = ExcesoCredito<int>();
            app.SetupSequence(a => a.InsertarAsync(It.IsAny<EntregaCrearDTO>()))
                .ReturnsAsync(exceso)
                .ReturnsAsync(new Respuesta<int> { Resultado = false, Mensaje = "otro" });

            var primero = await controller.InsertarAsync(new EntregaCrearDTO());
            var segundo = await controller.InsertarAsync(new EntregaCrearDTO());

            Assert.Same(exceso, Assert.IsType<ConflictObjectResult>(primero.Result).Value);
            Assert.IsType<BadRequestObjectResult>(segundo.Result);
        }

        // ---------- Factura: alta ----------

        private (FacturaController Controller, Mock<IFacturaApplication> App, AutorizacionCreditoContexto Ctx) CrearFactura(params Claim[] claims)
        {
            var app = new Mock<IFacturaApplication>();
            app.Setup(a => a.InsertarAsync(It.IsAny<FacturaCrearDTO>())).ReturnsAsync(new Respuesta<int> { Resultado = true, Dato = 5 });
            var ctx = new AutorizacionCreditoContexto();
            return (new FacturaController(app.Object, ctx) { ControllerContext = Contexto(claims) }, app, ctx);
        }

        [Fact]
        public async Task Factura_Insertar_PideAutorizarSinPermiso_403YNoLlamaALaAplicacion()
        {
            var (controller, app, ctx) = CrearFactura(Usuario("Facturas:VA"));

            var resultado = await controller.InsertarAsync(new FacturaCrearDTO { AutorizarExcesoCredito = true });

            AssertForbidden(resultado.Result as ObjectResult, MensajeSinAutorizar);
            app.Verify(a => a.InsertarAsync(It.IsAny<FacturaCrearDTO>()), Times.Never);
            Assert.False(ctx.Autorizada);
        }

        [Fact]
        public async Task Factura_Insertar_PideAutorizarConPermiso_LlenaElContexto()
        {
            var (controller, app, ctx) = CrearFactura(Usuario("Facturas:VA AutorizarExcesoCredito:V", nombre: "mgarcia"));

            var resultado = await controller.InsertarAsync(new FacturaCrearDTO { AutorizarExcesoCredito = true });

            Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.True(ctx.Autorizada);
            Assert.Equal("mgarcia", ctx.Usuario);
            app.Verify(a => a.InsertarAsync(It.IsAny<FacturaCrearDTO>()), Times.Once);
        }

        [Fact]
        public async Task Factura_Insertar_SinPedirAutorizar_NoTocaElContexto()
        {
            var (controller, _, ctx) = CrearFactura(Usuario("Facturas:VA AutorizarExcesoCredito:V"));

            await controller.InsertarAsync(new FacturaCrearDTO());

            Assert.False(ctx.Autorizada);
            Assert.Null(ctx.Usuario);
        }

        [Fact]
        public async Task Factura_Insertar_Exceso_Devuelve409YOtroErrorBadRequest()
        {
            var (controller, app, _) = CrearFactura(Usuario("Facturas:VA"));
            var exceso = ExcesoCredito<int>();
            app.SetupSequence(a => a.InsertarAsync(It.IsAny<FacturaCrearDTO>()))
                .ReturnsAsync(exceso)
                .ReturnsAsync(new Respuesta<int> { Resultado = false, Mensaje = "otro" });

            var primero = await controller.InsertarAsync(new FacturaCrearDTO());
            var segundo = await controller.InsertarAsync(new FacturaCrearDTO());

            Assert.Same(exceso, Assert.IsType<ConflictObjectResult>(primero.Result).Value);
            Assert.IsType<BadRequestObjectResult>(segundo.Result);
        }

        // ---------- Factura: pago inmediato ----------

        [Fact]
        public async Task Factura_Insertar_PagoInmediatoSinPermisoDePagos_403YNoLlamaALaAplicacion()
        {
            var (controller, app, _) = CrearFactura(Usuario("Facturas:VA PagosRecibidos:VU"));

            var resultado = await controller.InsertarAsync(new FacturaCrearDTO { PagoInmediato = 100m });

            AssertForbidden(resultado.Result as ObjectResult, MensajeSinPagos);
            app.Verify(a => a.InsertarAsync(It.IsAny<FacturaCrearDTO>()), Times.Never);
        }

        [Fact]
        public async Task Factura_Insertar_PagoInmediatoConPermisoDePagos_Procede()
        {
            var (controller, app, _) = CrearFactura(Usuario("Facturas:VA PagosRecibidos:VA"));

            var resultado = await controller.InsertarAsync(new FacturaCrearDTO { PagoInmediato = 100m });

            Assert.IsType<OkObjectResult>(resultado.Result);
            app.Verify(a => a.InsertarAsync(It.IsAny<FacturaCrearDTO>()), Times.Once);
        }

        [Fact]
        public async Task Factura_Insertar_SuperUsuarioPuedeRegistrarPagoInmediato()
        {
            var (controller, app, _) = CrearFactura(Usuario(superUsuario: "S"));

            var resultado = await controller.InsertarAsync(new FacturaCrearDTO { PagoInmediato = 100m });

            Assert.IsType<OkObjectResult>(resultado.Result);
            app.Verify(a => a.InsertarAsync(It.IsAny<FacturaCrearDTO>()), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0.0)]
        public async Task Factura_Insertar_SinPagoInmediatoPositivo_NoExigeElPermisoDePagos(double? pago)
        {
            var (controller, app, _) = CrearFactura(Usuario("Facturas:VA"));

            var resultado = await controller.InsertarAsync(new FacturaCrearDTO { PagoInmediato = pago.HasValue ? (decimal)pago.Value : null });

            Assert.IsType<OkObjectResult>(resultado.Result);
            app.Verify(a => a.InsertarAsync(It.IsAny<FacturaCrearDTO>()), Times.Once);
        }

        [Fact]
        public async Task Factura_Insertar_PagoInmediatoNegativo_PasaALaAplicacionQueLoRechaza()
        {
            var (controller, app, _) = CrearFactura(Usuario("Facturas:VA"));
            app.Setup(a => a.InsertarAsync(It.IsAny<FacturaCrearDTO>()))
                .ReturnsAsync(new Respuesta<int> { Resultado = false, Mensaje = "El pago inmediato no puede ser negativo." });

            var resultado = await controller.InsertarAsync(new FacturaCrearDTO { PagoInmediato = -5m });

            var mala = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Equal("El pago inmediato no puede ser negativo.", Assert.IsType<Respuesta<int>>(mala.Value).Mensaje);
        }
    }
}
