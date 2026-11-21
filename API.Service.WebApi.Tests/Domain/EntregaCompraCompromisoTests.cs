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
    /// La entrega de compra avisa al servicio de comprometido/pedido DESPUÉS de guardar (nunca dentro de la operación
    /// transaccional) y solo en el camino de éxito. EntregaCompraDomain no corre anidada en ninguna transacción exterior
    /// (solo la invoca su aplicación), así que el alta avisa siempre y el servicio retorna sin trabajar cuando ninguna línea
    /// copia un pedido de compra (BaseTipo 11). La garantía de que el aviso nunca falla la operación vive en el envoltorio del
    /// servicio (ExistenciaCompromisoService.Notificar*, probado en su propia clase): aquí solo se prueba que el dominio llama
    /// (y en qué orden), sin atrapar nada adicional. El caso con el servicio real queda para el humo con SQL real.
    /// </summary>
    public class EntregaCompraCompromisoTests
    {
        private readonly List<string> _orden = new();

        private readonly Mock<IRepositorioGenerico<EntregaCompra, int>> _repoEntrega = new();
        private readonly Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly Mock<IExistenciaCompromisoService> _compromisos = new();
        private readonly EntregaCompraDomain _domain;

        private static readonly IReadOnlyList<ParExistencia> Pares = new List<ParExistencia> { new("A", "01"), new("B", "02") };

        public EntregaCompraCompromisoTests()
        {
            _domain = new EntregaCompraDomain(_repoEntrega.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object, null, _compromisos.Object, Mock.Of<IDocumentoBaseCompraService>());

            var serie = new NumeracionDocumentoDet { CodigoObj = "12", Serie = 4, SigNumero = 5, Bloqueado = "N", Manual = "N", SubTipoDoc = "--", TipoSerie = "N", NombreSerie = "P" };
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoEntrega.Setup(r => r.InsertarAsync(It.IsAny<EntregaCompra>())).ReturnsAsync((EntregaCompra c) => { c.Entry = 99; return c; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<EntregaCompraDetalle>())).Returns(Task.CompletedTask);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(async f => { var r = await f(); _orden.Add("tx"); return r; });
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(async f => { var r = await f(); _orden.Add("tx"); return r; });

            _compromisos.Setup(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Callback<int, int>((t, e) => _orden.Add($"notificar:{t}:{e}")).Returns(Task.CompletedTask);
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(_ => _orden.Add("notificar-pares")).Returns(Task.CompletedTask);
            _compromisos.Setup(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Callback<int, int>((t, e) => _orden.Add($"obtener:{t}:{e}")).ReturnsAsync(Pares);
        }

        private static EntregaCompraDetalle Linea(int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            new() { CodArticulo = "A", CodAlmacen = "01", Cantidad = 4m, Precio = 10m, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea };

        private void SinNotificaciones()
        {
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
            _compromisos.Verify(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        private void ConEntregaGuardada(string estadoInv, string cancelado, params EntregaCompraDetalle[] lineas)
        {
            _repoEntrega.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new EntregaCompra { Entry = 1, EstadoInv = estadoInv, Cancelado = cancelado, CodigoSn = "PR-1" });
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.Select(l => { l.Entry = 1; return l; }).ToList().AsAsyncQueryable());
        }

        // ---------------- InsertarAsync ----------------

        [Fact]
        public async Task Insertar_ConBasePedidoCompra_NotificaUnaVez_DespuesDeLaTransaccion()
        {
            var entry = await _domain.InsertarAsync(new EntregaCompra { Serie = 4, CodigoSn = "PR-1" }, new[] { Linea(TipoDocCompromiso.PedidoCompra, 7, 1) });

            Assert.Equal(99, entry);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.EntregaCompra, 99), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
            Assert.Equal(new[] { "tx", "notificar:12:99" }, _orden);
        }

        [Fact]
        public async Task Insertar_Directa_SinBase_NotificaUnaVez_ElServicioRetornaSinTrabajar()
        {
            var entry = await _domain.InsertarAsync(new EntregaCompra { Serie = 4, CodigoSn = "PR-1" }, new[] { Linea(), Linea() });

            Assert.Equal(99, entry);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.EntregaCompra, 99), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
            Assert.Equal(new[] { "tx", "notificar:12:99" }, _orden);
        }

        [Fact]
        public async Task Insertar_BasePedidoEnVariasLineas_NotificaUnaVez()
        {
            await _domain.InsertarAsync(new EntregaCompra { Serie = 4, CodigoSn = "PR-1" },
                new[] { Linea(), Linea(TipoDocCompromiso.PedidoCompra, 7, 2), Linea(TipoDocCompromiso.PedidoCompra, 7, 3) });

            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task Insertar_SerieInexistente_Lanza_YNoNotifica()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null!);

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompra { Serie = 4, CodigoSn = "PR-1" }, new[] { Linea(TipoDocCompromiso.PedidoCompra, 7, 1) }));

            SinNotificaciones();
        }

        [Fact]
        public async Task Insertar_SerieBloqueada_Lanza_YNoNotifica()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(new NumeracionDocumentoDet { CodigoObj = "12", Serie = 4, SigNumero = 5, Bloqueado = "S", Manual = "N" });

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompra { Serie = 4, CodigoSn = "PR-1" }, new[] { Linea(TipoDocCompromiso.PedidoCompra, 7, 1) }));

            SinNotificaciones();
        }

        [Fact]
        public async Task Insertar_FallaLaTransaccion_Lanza_YNoNotifica()
        {
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).ThrowsAsync(new Exception("fallo al guardar"));

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompra { Serie = 4, CodigoSn = "PR-1" }, new[] { Linea(TipoDocCompromiso.PedidoCompra, 7, 1) }));

            SinNotificaciones();
        }

        // ---------------- ActualizarAsync ----------------

        [Fact]
        public async Task Actualizar_Cancelar_NotificaUnaVez_DespuesDeLaTransaccion()
        {
            ConEntregaGuardada("A", "N", Linea(TipoDocCompromiso.PedidoCompra, 7, 1));

            Assert.True(await _domain.ActualizarAsync(1, new EntregaCompra { Cancelado = "S" }));

            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.EntregaCompra, 1), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
            Assert.Equal(new[] { "tx", "notificar:12:1" }, _orden);
        }

        [Fact]
        public async Task Actualizar_SoloComentario_NoCambiaElEstado_YNoNotifica()
        {
            ConEntregaGuardada("A", "N", Linea(TipoDocCompromiso.PedidoCompra, 7, 1));

            Assert.True(await _domain.ActualizarAsync(1, new EntregaCompra { Cancelado = "N", Comentario = "nota" }));

            SinNotificaciones();
        }

        [Fact]
        public async Task Actualizar_Inexistente_DevuelveFalse_YNoNotifica()
        {
            _repoEntrega.Setup(r => r.ObtenerAsync(1)).ReturnsAsync((EntregaCompra?)null!);

            Assert.False(await _domain.ActualizarAsync(1, new EntregaCompra { Cancelado = "S" }));

            SinNotificaciones();
        }

        [Fact]
        public async Task Actualizar_YaCancelada_Lanza_YNoNotifica()
        {
            ConEntregaGuardada("C", "S");

            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(1, new EntregaCompra { Cancelado = "S" }));

            SinNotificaciones();
        }

        [Fact]
        public async Task Actualizar_Cancelar_FallaLaTransaccion_Lanza_YNoNotifica()
        {
            ConEntregaGuardada("A", "N", Linea(TipoDocCompromiso.PedidoCompra, 7, 1));
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).ThrowsAsync(new Exception("fallo al guardar"));

            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(1, new EntregaCompra { Cancelado = "S" }));

            SinNotificaciones();
        }

        // ---------------- EliminarAsync ----------------

        [Fact]
        public async Task Eliminar_ObtieneLosParesAntesDeBorrar_YNotificaEsosParesDespues()
        {
            ConEntregaGuardada("C", "S", Linea(TipoDocCompromiso.PedidoCompra, 7, 1), Linea(TipoDocCompromiso.PedidoCompra, 7, 2));
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).Callback(() => _orden.Add("borrar-linea")).ReturnsAsync(true);
            _repoEntrega.Setup(r => r.EliminarAsync(1)).Callback(() => _orden.Add("borrar-cabecera")).ReturnsAsync(true);
            IReadOnlyCollection<ParExistencia>? recibidos = null;
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(p => { recibidos = p; _orden.Add("notificar-pares"); }).Returns(Task.CompletedTask);

            Assert.True(await _domain.EliminarAsync(1));

            Assert.Equal(new[] { "obtener:12:1", "borrar-linea", "borrar-linea", "borrar-cabecera", "tx", "notificar-pares" }, _orden);
            Assert.Same(Pares, recibidos);
            _compromisos.Verify(c => c.ObtenerParesAsync(TipoDocCompromiso.EntregaCompra, 1), Times.Once);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Eliminar_DevuelveFalse_NoNotifica()
        {
            ConEntregaGuardada("C", "S");
            _repoEntrega.Setup(r => r.EliminarAsync(1)).ReturnsAsync(false);

            Assert.False(await _domain.EliminarAsync(1));

            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Eliminar_SinCancelarPrimero_Lanza_YNoConsultaNiNotifica()
        {
            ConEntregaGuardada("A", "N");

            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(1));

            SinNotificaciones();
        }

        [Fact]
        public async Task Eliminar_Inexistente_DevuelveFalse_YNoConsultaNiNotifica()
        {
            _repoEntrega.Setup(r => r.ObtenerAsync(1)).ReturnsAsync((EntregaCompra?)null!);

            Assert.False(await _domain.EliminarAsync(1));

            SinNotificaciones();
        }

        [Fact]
        public async Task Eliminar_FallaLaTransaccion_Lanza_YNoNotificaPares()
        {
            ConEntregaGuardada("C", "S");
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).ThrowsAsync(new Exception("fallo al guardar"));

            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(1));

            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
        }
    }
}
