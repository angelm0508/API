using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Existencias;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>
    /// Pedido de venta y su línea avisan al servicio de comprometido/pedido DESPUÉS de guardar (nunca dentro de la
    /// operación transaccional) y solo en el camino de éxito. La garantía de que el aviso nunca falla la operación
    /// vive en el envoltorio del servicio (ExistenciaCompromisoService.Notificar*, probado en su propia clase): aquí
    /// solo se prueba que el dominio llama (y en qué orden), sin atrapar nada adicional.
    /// </summary>
    public class PedidoCompromisoTests
    {
        private readonly List<string> _orden = new();

        private readonly Mock<IRepositorioGenerico<Pedido, int>> _repoPedido = new();
        private readonly Mock<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<AutorizacionCredito, int>> _repoAutorizacion = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly Mock<ICreditoClienteService> _credito = new();
        private readonly AutorizacionCreditoContexto _contexto = new();
        private readonly Mock<IExistenciaCompromisoService> _compromisos = new();
        private readonly PedidoDomain _pedido;
        private readonly PedidoDetalleDomain _detalle;

        private static readonly PosicionCredito Posicion = new("CL-1", 1000m, false, 600m, 0m, 300m, 900m, 100m);
        private static readonly ResultadoCredito Permitido = new(true, null, Posicion, 50m, 0m);
        private static readonly ResultadoCredito Rechazado = new(false, "LIMITE", Posicion, 400m, 300m);

        private static readonly IReadOnlyList<ParExistencia> Pares = new List<ParExistencia> { new("A", "01"), new("B", "02") };

        public PedidoCompromisoTests()
        {
            _pedido = new PedidoDomain(_repoPedido.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object,
                _documentoBase.Object, _credito.Object, _contexto, _repoAutorizacion.Object, null, _compromisos.Object);
            _detalle = new PedidoDetalleDomain(_repoDetalle.Object, _documentoBase.Object, null, _compromisos.Object);

            var serie = new NumeracionDocumentoDet { CodigoObj = "4", Serie = 4, SigNumero = 5, Bloqueado = "N", Manual = "N", SubTipoDoc = "--", TipoSerie = "N", NombreSerie = "P" };
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoPedido.Setup(r => r.InsertarAsync(It.IsAny<Pedido>())).ReturnsAsync((Pedido c) => { c.Entry = 99; return c; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<PedidoDetalle>())).Returns(Task.CompletedTask);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(async f => { var r = await f(); _orden.Add("tx"); return r; });
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(async f => { var r = await f(); _orden.Add("tx"); return r; });

            _credito.Setup(c => c.CalcularMontoAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>())).ReturnsAsync(50m);
            _credito.Setup(c => c.ValorAbiertoDocumentoAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(50m);
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>())).ReturnsAsync(Permitido);

            _compromisos.Setup(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Callback<int, int>((t, e) => _orden.Add($"notificar:{t}:{e}")).Returns(Task.CompletedTask);
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(_ => _orden.Add("notificar-pares")).Returns(Task.CompletedTask);
            _compromisos.Setup(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Callback<int, int>((t, e) => _orden.Add($"obtener:{t}:{e}")).ReturnsAsync(Pares);
        }

        private static PedidoDetalle Linea(string art = "A") => new() { CodArticulo = art, Cantidad = 1m, Precio = 10m };

        private void SinNotificaciones()
        {
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
            _compromisos.Verify(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        private void ConPedidoGuardado(string estadoDoc, string cancelado, params PedidoDetalle[] lineas)
        {
            _repoPedido.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Pedido { Entry = 1, EstadoDoc = estadoDoc, Cancelado = cancelado, CodigoSn = "CL-1" });
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.Select(l => { l.Entry = 1; return l; }).ToList().AsAsyncQueryable());
        }

        // ---------------- PedidoDomain.InsertarAsync ----------------

        [Fact]
        public async Task Insertar_ConLineas_NotificaUnaVez_DespuesDeLaTransaccion()
        {
            var entry = await _pedido.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea() });

            Assert.Equal(99, entry);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.Pedido, 99), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
            Assert.Equal(new[] { "tx", "notificar:4:99" }, _orden);
        }

        [Fact]
        public async Task Insertar_SinLineas_NoNotifica()
        {
            var entry = await _pedido.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1" });

            Assert.Equal(99, entry);
            SinNotificaciones();
        }

        [Fact]
        public async Task Insertar_ConLineas_SerieInexistente_Lanza_YNoNotifica()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null!);

            await Assert.ThrowsAsync<Exception>(() => _pedido.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea() }));

            SinNotificaciones();
        }

        [Fact]
        public async Task Insertar_ConLineas_LimiteDeCreditoRechazado_Lanza_YNoNotifica()
        {
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>())).ReturnsAsync(Rechazado);

            await Assert.ThrowsAsync<CreditoExcedidoException>(() => _pedido.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea() }));

            SinNotificaciones();
        }

        [Fact]
        public async Task Insertar_ConLineas_FallaLaTransaccion_Lanza_YNoNotifica()
        {
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).ThrowsAsync(new Exception("fallo al guardar"));

            await Assert.ThrowsAsync<Exception>(() => _pedido.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea() }));

            SinNotificaciones();
        }

        // ---------------- PedidoDomain.ActualizarAsync ----------------

        [Theory]
        [InlineData("A", "N", "C", "N")] // cancelar (EstadoDoc)
        [InlineData("A", "N", "A", "S")] // cancelar (Cancelado)
        [InlineData("C", "N", "A", "N")] // reabrir
        [InlineData("A", "N", "A", "N")] // sin cambio de estado (idempotente)
        public async Task Actualizar_Exito_NotificaUnaVez_DespuesDeGuardar(string estadoAntes, string canceladoAntes, string estadoNuevo, string canceladoNuevo)
        {
            ConPedidoGuardado(estadoAntes, canceladoAntes, Linea());
            _repoPedido.Setup(r => r.ActualizarAsync(1, It.IsAny<Pedido>())).Callback(() => _orden.Add("guardar")).ReturnsAsync(true);

            Assert.True(await _pedido.ActualizarAsync(1, new Pedido { EstadoDoc = estadoNuevo, Cancelado = canceladoNuevo, CodigoSn = "CL-1" }));

            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.Pedido, 1), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
            Assert.Equal(new[] { "guardar", "notificar:4:1" }, _orden);
        }

        [Fact]
        public async Task Actualizar_ConAutorizacionDeCredito_NotificaDespuesDeLaTransaccion()
        {
            ConPedidoGuardado("C", "N", Linea());
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>())).ReturnsAsync(Rechazado);
            _contexto.Autorizada = true;
            _contexto.Usuario = "admin";
            _repoPedido.Setup(r => r.ActualizarAsync(1, It.IsAny<Pedido>())).Callback(() => _orden.Add("guardar")).ReturnsAsync(true);

            Assert.True(await _pedido.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-1" }));

            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.Pedido, 1), Times.Once);
            Assert.Equal(new[] { "guardar", "tx", "notificar:4:1" }, _orden);
        }

        [Fact]
        public async Task Actualizar_DevuelveFalse_NoNotifica()
        {
            ConPedidoGuardado("A", "N", Linea());
            _repoPedido.Setup(r => r.ActualizarAsync(1, It.IsAny<Pedido>())).ReturnsAsync(false);

            Assert.False(await _pedido.ActualizarAsync(1, new Pedido { EstadoDoc = "C", CodigoSn = "CL-1" }));

            SinNotificaciones();
        }

        [Fact]
        public async Task Actualizar_OrigenNoModificable_Lanza_YNoNotifica()
        {
            ConPedidoGuardado("A", "N", Linea());
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Pedido, 1, "cancelar el documento"))
                .ThrowsAsync(new Exception("tiene derivados"));

            await Assert.ThrowsAsync<Exception>(() => _pedido.ActualizarAsync(1, new Pedido { EstadoDoc = "C", CodigoSn = "CL-1" }));

            SinNotificaciones();
        }

        [Fact]
        public async Task Actualizar_CreditoRechazado_Lanza_YNoNotifica()
        {
            ConPedidoGuardado("C", "N", Linea());
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>())).ReturnsAsync(Rechazado);

            await Assert.ThrowsAsync<CreditoExcedidoException>(() => _pedido.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-1" }));

            SinNotificaciones();
        }

        // ---------------- PedidoDomain.EliminarAsync ----------------

        [Fact]
        public async Task Eliminar_ObtieneLosParesAntesDeBorrar_YNotificaEsosParesDespues()
        {
            ConPedidoGuardado("A", "N", Linea("A"), Linea("B"));
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).Callback(() => _orden.Add("borrar-linea")).ReturnsAsync(true);
            _repoPedido.Setup(r => r.EliminarAsync(1)).Callback(() => _orden.Add("borrar-cabecera")).ReturnsAsync(true);
            IReadOnlyCollection<ParExistencia>? recibidos = null;
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(p => { recibidos = p; _orden.Add("notificar-pares"); }).Returns(Task.CompletedTask);

            Assert.True(await _pedido.EliminarAsync(1));

            Assert.Equal(new[] { "obtener:4:1", "borrar-linea", "borrar-linea", "borrar-cabecera", "notificar-pares" }, _orden);
            Assert.Same(Pares, recibidos);
            _compromisos.Verify(c => c.ObtenerParesAsync(TipoDocCompromiso.Pedido, 1), Times.Once);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Eliminar_DevuelveFalse_NoNotifica()
        {
            ConPedidoGuardado("A", "N");
            _repoPedido.Setup(r => r.EliminarAsync(1)).ReturnsAsync(false);

            Assert.False(await _pedido.EliminarAsync(1));

            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Eliminar_OrigenNoModificable_Lanza_YNoConsultaNiNotifica()
        {
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Pedido, 1, "eliminar el documento"))
                .ThrowsAsync(new Exception("tiene derivados"));

            await Assert.ThrowsAsync<Exception>(() => _pedido.EliminarAsync(1));

            SinNotificaciones();
        }

        // ---------------- PedidoDetalleDomain ----------------

        [Fact]
        public async Task Detalle_Insertar_NotificaUnaVez_DespuesDeGuardar()
        {
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoDetalle>().AsAsyncQueryable());
            _repoDetalle.Setup(r => r.InsertarAsync(It.IsAny<PedidoDetalle>())).Callback(() => _orden.Add("guardar")).ReturnsAsync((PedidoDetalle l) => l);

            var noLinea = await _detalle.InsertarAsync(new PedidoDetalle { Entry = 5, BaseTipo = -1 });

            Assert.Equal(1, noLinea);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.Pedido, 5), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
            Assert.Equal(new[] { "guardar", "notificar:4:5" }, _orden);
        }

        [Fact]
        public async Task Detalle_Insertar_LineaConBase_Lanza_YNoNotifica()
        {
            await Assert.ThrowsAsync<Exception>(() => _detalle.InsertarAsync(new PedidoDetalle { Entry = 5, BaseTipo = 3, BaseEntry = 1, BaseLinea = 1 }));

            SinNotificaciones();
        }

        [Fact]
        public async Task Detalle_Actualizar_NotificaUnaVez_DespuesDeGuardar()
        {
            _repoDetalle.Setup(r => r.ActualizarAsync(It.IsAny<(int Entry, int NoLinea)>(), It.IsAny<PedidoDetalle>())).Callback(() => _orden.Add("guardar")).ReturnsAsync(true);

            Assert.True(await _detalle.ActualizarAsync(5, 2, new PedidoDetalle { Entry = 5, NoLinea = 2 }));

            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.Pedido, 5), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
            Assert.Equal(new[] { "guardar", "notificar:4:5" }, _orden);
        }

        [Fact]
        public async Task Detalle_Actualizar_DevuelveFalse_NoNotifica()
        {
            _repoDetalle.Setup(r => r.ActualizarAsync(It.IsAny<(int Entry, int NoLinea)>(), It.IsAny<PedidoDetalle>())).ReturnsAsync(false);

            Assert.False(await _detalle.ActualizarAsync(5, 2, new PedidoDetalle()));

            SinNotificaciones();
        }

        [Fact]
        public async Task Detalle_Actualizar_LineaConsumida_Lanza_YNoNotifica()
        {
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Pedido, 5, 2, "modificar"))
                .ThrowsAsync(new Exception("la usan Entrega 3"));

            await Assert.ThrowsAsync<Exception>(() => _detalle.ActualizarAsync(5, 2, new PedidoDetalle()));

            SinNotificaciones();
        }

        [Fact]
        public async Task Detalle_Eliminar_NotificaUnaVez_DespuesDeBorrar()
        {
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).Callback(() => _orden.Add("borrar")).ReturnsAsync(true);

            Assert.True(await _detalle.EliminarAsync(5, 2));

            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.Pedido, 5), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
            Assert.Equal(new[] { "borrar", "notificar:4:5" }, _orden);
        }

        [Fact]
        public async Task Detalle_Eliminar_DevuelveFalse_NoNotifica()
        {
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).ReturnsAsync(false);

            Assert.False(await _detalle.EliminarAsync(5, 2));

            SinNotificaciones();
        }

        [Fact]
        public async Task Detalle_Eliminar_LineaConsumida_Lanza_YNoNotifica()
        {
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Pedido, 5, 2, "eliminar"))
                .ThrowsAsync(new Exception("la usan Entrega 3"));

            await Assert.ThrowsAsync<Exception>(() => _detalle.EliminarAsync(5, 2));

            SinNotificaciones();
        }
    }
}
