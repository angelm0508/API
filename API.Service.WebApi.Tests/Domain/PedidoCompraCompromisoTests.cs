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
    /// Pedido de compra y su línea avisan al servicio de existencias (columna Pedido) DESPUÉS de guardar y solo en
    /// el camino de éxito. La garantía de que el aviso nunca falla la operación vive en el envoltorio del servicio
    /// (ExistenciaCompromisoService.Notificar*, probado en su propia clase): aquí solo se prueba que el dominio llama
    /// (y en qué orden), sin atrapar nada adicional.
    /// </summary>
    public class PedidoCompraCompromisoTests
    {
        private readonly List<string> _orden = new();

        private readonly Mock<IRepositorioGenerico<PedidoCompra, int>> _repoPedido = new();
        private readonly Mock<IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<EntregaCompra, int>> _repoEntrega = new();
        private readonly Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>> _repoEntregaDet = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>> _repoFacturaDet = new();
        private readonly Mock<IExistenciaCompromisoService> _compromisos = new();
        private readonly PedidoCompraDomain _pedido;
        private readonly PedidoCompraDetalleDomain _detalle;

        private static readonly IReadOnlyList<ParExistencia> Pares = new List<ParExistencia> { new("A", "01"), new("B", "02") };

        public PedidoCompraCompromisoTests()
        {
            _pedido = new PedidoCompraDomain(_repoPedido.Object, _repoDetalle.Object, _repoNumeracion.Object,
                _repoEntrega.Object, _repoEntregaDet.Object, _repoFactura.Object, _repoFacturaDet.Object, _compromisos.Object,
                Mock.Of<IDocumentoBaseCompraService>());
            _detalle = new PedidoCompraDetalleDomain(_repoDetalle.Object, null, _compromisos.Object, Mock.Of<IDocumentoBaseCompraService>());

            var serie = new NumeracionDocumentoDet { CodigoObj = "11", Serie = 4, SigNumero = 5, Bloqueado = "N", Manual = "N", SubTipoDoc = "--", TipoSerie = "N", NombreSerie = "P" };
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoPedido.Setup(r => r.InsertarAsync(It.IsAny<PedidoCompra>())).ReturnsAsync((PedidoCompra c) => { c.Entry = 99; return c; });

            _compromisos.Setup(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Callback<int, int>((t, e) => _orden.Add($"notificar:{t}:{e}")).Returns(Task.CompletedTask);
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(_ => _orden.Add("notificar-pares")).Returns(Task.CompletedTask);
            _compromisos.Setup(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Callback<int, int>((t, e) => _orden.Add($"obtener:{t}:{e}")).ReturnsAsync(Pares);
        }

        private static PedidoCompraDetalle Linea(string art = "A") => new() { CodArticulo = art, Cantidad = 1m, Precio = 10m };

        private void SinNotificaciones()
        {
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
            _compromisos.Verify(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        private void ConPedidoGuardado(string cancelado, params PedidoCompraDetalle[] lineas)
        {
            _repoPedido.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new PedidoCompra { Entry = 1, Cancelado = cancelado, CodigoSn = "PR-1", Nit = "123" });
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.Select(l => { l.Entry = 1; return l; }).ToList().AsAsyncQueryable());
        }

        // ---------------- PedidoCompraDomain.InsertarAsync (solo cabecera) ----------------

        [Fact]
        public async Task Insertar_SoloCabecera_NoNotifica()
        {
            var entry = await _pedido.InsertarAsync(new PedidoCompra { Serie = 4, CodigoSn = "PR-1" });

            Assert.Equal(99, entry);
            SinNotificaciones();
        }

        // ---------------- PedidoCompraDomain.ActualizarAsync ----------------

        [Theory]
        [InlineData("N")] // edición normal / reabrir
        [InlineData("S")] // cancelar
        public async Task Actualizar_Exito_NotificaUnaVez_DespuesDeGuardar(string cancelado)
        {
            ConPedidoGuardado("N", Linea());
            _repoPedido.Setup(r => r.ActualizarAsync(1, It.IsAny<PedidoCompra>())).Callback(() => _orden.Add("guardar")).ReturnsAsync(true);

            Assert.True(await _pedido.ActualizarAsync(1, new PedidoCompra { Cancelado = cancelado, CodigoSn = "PR-1" }));

            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.PedidoCompra, 1), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
            Assert.Equal(new[] { "guardar", "notificar:11:1" }, _orden);
        }

        [Fact]
        public async Task Actualizar_ConservaElNit_YNotifica()
        {
            ConPedidoGuardado("N");
            PedidoCompra? guardado = null;
            _repoPedido.Setup(r => r.ActualizarAsync(1, It.IsAny<PedidoCompra>())).Callback<int, PedidoCompra>((_, o) => guardado = o).ReturnsAsync(true);

            Assert.True(await _pedido.ActualizarAsync(1, new PedidoCompra { Nit = "otro" }));

            Assert.Equal("123", guardado!.Nit);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.PedidoCompra, 1), Times.Once);
        }

        [Fact]
        public async Task Actualizar_DevuelveFalse_NoNotifica()
        {
            ConPedidoGuardado("N");
            _repoPedido.Setup(r => r.ActualizarAsync(1, It.IsAny<PedidoCompra>())).ReturnsAsync(false);

            Assert.False(await _pedido.ActualizarAsync(1, new PedidoCompra { Cancelado = "S" }));

            SinNotificaciones();
        }

        [Fact]
        public async Task Actualizar_Lanza_NoNotifica()
        {
            ConPedidoGuardado("N");
            _repoPedido.Setup(r => r.ActualizarAsync(1, It.IsAny<PedidoCompra>())).ThrowsAsync(new Exception("fallo al guardar"));

            await Assert.ThrowsAsync<Exception>(() => _pedido.ActualizarAsync(1, new PedidoCompra()));

            SinNotificaciones();
        }

        // ---------------- PedidoCompraDomain.EliminarAsync ----------------

        [Fact]
        public async Task Eliminar_ObtieneLosParesAntesDeBorrar_YNotificaEsosParesDespues()
        {
            ConPedidoGuardado("N", Linea("A"), Linea("B"));
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).Callback(() => _orden.Add("borrar-linea")).ReturnsAsync(true);
            _repoPedido.Setup(r => r.EliminarAsync(1)).Callback(() => _orden.Add("borrar-cabecera")).ReturnsAsync(true);
            IReadOnlyCollection<ParExistencia>? recibidos = null;
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(p => { recibidos = p; _orden.Add("notificar-pares"); }).Returns(Task.CompletedTask);

            Assert.True(await _pedido.EliminarAsync(1));

            Assert.Equal(new[] { "obtener:11:1", "borrar-linea", "borrar-linea", "borrar-cabecera", "notificar-pares" }, _orden);
            Assert.Same(Pares, recibidos);
            _compromisos.Verify(c => c.ObtenerParesAsync(TipoDocCompromiso.PedidoCompra, 1), Times.Once);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Eliminar_DevuelveFalse_NoNotifica()
        {
            ConPedidoGuardado("N");
            _repoPedido.Setup(r => r.EliminarAsync(1)).ReturnsAsync(false);

            Assert.False(await _pedido.EliminarAsync(1));

            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Eliminar_LineasBorradasYCabeceraDevuelveFalse_NotificaLosParesPrevios()
        {
            ConPedidoGuardado("N", Linea("A"), Linea("B"));
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).Callback(() => _orden.Add("borrar-linea")).ReturnsAsync(true);
            _repoPedido.Setup(r => r.EliminarAsync(1)).Callback(() => _orden.Add("borrar-cabecera")).ReturnsAsync(false);
            IReadOnlyCollection<ParExistencia>? recibidos = null;
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(p => { recibidos = p; _orden.Add("notificar-pares"); }).Returns(Task.CompletedTask);

            Assert.False(await _pedido.EliminarAsync(1));

            Assert.Equal(new[] { "obtener:11:1", "borrar-linea", "borrar-linea", "borrar-cabecera", "notificar-pares" }, _orden);
            Assert.Same(Pares, recibidos);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Once);
        }

        [Fact]
        public async Task Eliminar_CabeceraLanza_DespuesDeBorrarLineas_PropagaYNoNotifica()
        {
            ConPedidoGuardado("N", Linea("A"));
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).ReturnsAsync(true);
            _repoPedido.Setup(r => r.EliminarAsync(1)).ThrowsAsync(new Exception("fallo al borrar"));

            await Assert.ThrowsAsync<Exception>(() => _pedido.EliminarAsync(1));

            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
        }

        [Fact]
        public async Task Eliminar_FallaElBorrado_Lanza_YNoNotifica()
        {
            ConPedidoGuardado("N", Linea());
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).ThrowsAsync(new Exception("fallo al borrar"));

            await Assert.ThrowsAsync<Exception>(() => _pedido.EliminarAsync(1));

            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        // ---------------- PedidoCompraDetalleDomain ----------------

        [Fact]
        public async Task Detalle_Insertar_NotificaUnaVez_DespuesDeGuardar()
        {
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle>().AsAsyncQueryable());
            _repoDetalle.Setup(r => r.InsertarAsync(It.IsAny<PedidoCompraDetalle>())).Callback(() => _orden.Add("guardar")).ReturnsAsync((PedidoCompraDetalle l) => l);

            var noLinea = await _detalle.InsertarAsync(new PedidoCompraDetalle { Entry = 5 });

            Assert.Equal(1, noLinea);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.PedidoCompra, 5), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
            Assert.Equal(new[] { "guardar", "notificar:11:5" }, _orden);
        }

        [Fact]
        public async Task Detalle_Insertar_Lanza_NoNotifica()
        {
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle>().AsAsyncQueryable());
            _repoDetalle.Setup(r => r.InsertarAsync(It.IsAny<PedidoCompraDetalle>())).ThrowsAsync(new Exception("fallo al guardar"));

            await Assert.ThrowsAsync<Exception>(() => _detalle.InsertarAsync(new PedidoCompraDetalle { Entry = 5 }));

            SinNotificaciones();
        }

        // Tras guardar no se puede reconstruir el par viejo de la línea (borrada o con artículo/almacén cambiado):
        // los pares se leen ANTES de guardar y se avisan además de los del documento ya guardado.
        private void SinAvisos()
        {
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
        }

        [Fact]
        public async Task Detalle_Actualizar_LeeParesAntes_YAvisaParesViejosYDocumentoDespuesDeGuardar()
        {
            _repoDetalle.Setup(r => r.ActualizarAsync(It.IsAny<(int Entry, int NoLinea)>(), It.IsAny<PedidoCompraDetalle>())).Callback(() => _orden.Add("actualizar-linea")).ReturnsAsync(true);
            IReadOnlyCollection<ParExistencia>? recibidos = null;
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(p => { recibidos = p; _orden.Add("notificar-pares"); }).Returns(Task.CompletedTask);

            Assert.True(await _detalle.ActualizarAsync(5, 2, new PedidoCompraDetalle { Entry = 5, NoLinea = 2 }));

            Assert.Equal(new[] { "obtener:11:5", "actualizar-linea", "notificar-pares", "notificar:11:5" }, _orden);
            Assert.Same(Pares, recibidos);
            _compromisos.Verify(c => c.ObtenerParesAsync(TipoDocCompromiso.PedidoCompra, 5), Times.Once);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(TipoDocCompromiso.PedidoCompra, 5), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task Detalle_Actualizar_CambiaDeArticulo_ElParViejoSeAvisa()
        {
            // Fake con estado: ObtenerPares devuelve los pares de las líneas tal como están en ese instante.
            var lineas = new List<ParExistencia> { new("A", "01") };
            _compromisos.Setup(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(() => (IReadOnlyList<ParExistencia>)lineas.ToList());
            _repoDetalle.Setup(r => r.ActualizarAsync(It.IsAny<(int Entry, int NoLinea)>(), It.IsAny<PedidoCompraDetalle>()))
                .Callback(() => { lineas.Clear(); lineas.Add(new("B", "01")); }).ReturnsAsync(true);
            IReadOnlyCollection<ParExistencia>? recibidos = null;
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(p => recibidos = p).Returns(Task.CompletedTask);

            Assert.True(await _detalle.ActualizarAsync(5, 2, new PedidoCompraDetalle { Entry = 5, NoLinea = 2, CodArticulo = "B" }));

            Assert.Equal(new[] { new ParExistencia("A", "01") }, recibidos);
        }

        [Fact]
        public async Task Detalle_Actualizar_DevuelveFalse_NoAvisa()
        {
            _repoDetalle.Setup(r => r.ActualizarAsync(It.IsAny<(int Entry, int NoLinea)>(), It.IsAny<PedidoCompraDetalle>())).ReturnsAsync(false);

            Assert.False(await _detalle.ActualizarAsync(5, 2, new PedidoCompraDetalle()));

            SinAvisos();
        }

        [Fact]
        public async Task Detalle_Actualizar_Lanza_NoAvisa()
        {
            _repoDetalle.Setup(r => r.ActualizarAsync(It.IsAny<(int Entry, int NoLinea)>(), It.IsAny<PedidoCompraDetalle>())).ThrowsAsync(new Exception("fallo al guardar"));

            await Assert.ThrowsAsync<Exception>(() => _detalle.ActualizarAsync(5, 2, new PedidoCompraDetalle()));

            SinAvisos();
        }

        [Fact]
        public async Task Detalle_Eliminar_LeeParesAntesDeBorrar_YAvisaEsosParesDespues()
        {
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).Callback(() => _orden.Add("borrar-linea")).ReturnsAsync(true);
            IReadOnlyCollection<ParExistencia>? recibidos = null;
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(p => { recibidos = p; _orden.Add("notificar-pares"); }).Returns(Task.CompletedTask);

            Assert.True(await _detalle.EliminarAsync(5, 2));

            Assert.Equal(new[] { "obtener:11:5", "borrar-linea", "notificar-pares" }, _orden);
            Assert.Same(Pares, recibidos);
            _compromisos.Verify(c => c.ObtenerParesAsync(TipoDocCompromiso.PedidoCompra, 5), Times.Once);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Once);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Detalle_Eliminar_UnicaLineaDelPar_ElParBorradoSeAvisa()
        {
            // Fake con estado: ObtenerPares devuelve los pares de las líneas tal como están en ese instante.
            var lineas = new List<ParExistencia> { new("A", "01") };
            _compromisos.Setup(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(() => (IReadOnlyList<ParExistencia>)lineas.ToList());
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).Callback(() => lineas.Clear()).ReturnsAsync(true);
            IReadOnlyCollection<ParExistencia>? recibidos = null;
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(p => recibidos = p).Returns(Task.CompletedTask);

            Assert.True(await _detalle.EliminarAsync(5, 2));

            Assert.Empty(lineas);
            Assert.Equal(new[] { new ParExistencia("A", "01") }, recibidos);
        }

        [Fact]
        public async Task Detalle_Eliminar_DevuelveFalse_NoAvisa()
        {
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).ReturnsAsync(false);

            Assert.False(await _detalle.EliminarAsync(5, 2));

            SinAvisos();
        }

        [Fact]
        public async Task Detalle_Eliminar_Lanza_NoAvisa()
        {
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).ThrowsAsync(new Exception("fallo al borrar"));

            await Assert.ThrowsAsync<Exception>(() => _detalle.EliminarAsync(5, 2));

            SinAvisos();
        }
    }
}
