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
    /// Integridad del origen (Copiar a en compras): un pedido de compra con destinos activos no se cancela, no cambia de
    /// proveedor ni se elimina. La validacion va PRIMERO: antes de leer los pares de Comprometido/Pedido, de cualquier cambio
    /// y de cualquier aviso; un rechazo no deja rastro.
    /// </summary>
    public class PedidoCompraIntegridadTests
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
        private readonly Mock<IDocumentoBaseCompraService> _documentoBase = new();
        private readonly PedidoCompraDomain _domain;

        private static readonly IReadOnlyList<ParExistencia> Pares = new List<ParExistencia> { new("A", "01") };

        public PedidoCompraIntegridadTests()
        {
            _domain = new PedidoCompraDomain(_repoPedido.Object, _repoDetalle.Object, _repoNumeracion.Object,
                _repoEntrega.Object, _repoEntregaDet.Object, _repoFactura.Object, _repoFacturaDet.Object,
                _compromisos.Object, _documentoBase.Object);

            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback<int, int, string>((t, e, a) => _orden.Add($"validar:{t}:{e}:{a}")).Returns(Task.CompletedTask);

            _compromisos.Setup(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Callback<int, int>((t, e) => _orden.Add($"notificar:{t}:{e}")).Returns(Task.CompletedTask);
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(_ => _orden.Add("notificar-pares")).Returns(Task.CompletedTask);
            _compromisos.Setup(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Callback<int, int>((t, e) => _orden.Add($"obtener:{t}:{e}")).ReturnsAsync(Pares);

            _repoPedido.Setup(r => r.ActualizarAsync(1, It.IsAny<PedidoCompra>())).Callback(() => _orden.Add("guardar")).ReturnsAsync(true);
            _repoPedido.Setup(r => r.EliminarAsync(1)).Callback(() => _orden.Add("borrar-cabecera")).ReturnsAsync(true);
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).Callback(() => _orden.Add("borrar-linea")).ReturnsAsync(true);
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(
                new List<PedidoCompraDetalle> { new() { Entry = 1, NoLinea = 1, CodArticulo = "A" } }.AsAsyncQueryable());
        }

        private void ConPedidoGuardado(string estadoDoc = "O", string cancelado = "N", string codigoSn = "PR-1") =>
            _repoPedido.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(
                new PedidoCompra { Entry = 1, EstadoDoc = estadoDoc, Cancelado = cancelado, CodigoSn = codigoSn, Nit = "123" });

        private void SinAvisosNiCambios()
        {
            _repoPedido.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<PedidoCompra>()), Times.Never);
            _repoPedido.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
            _repoDetalle.Verify(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>()), Times.Never);
            _compromisos.Verify(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
        }

        // ---------------- ActualizarAsync ----------------

        [Theory]
        [InlineData("C", "N")] // cancelar por EstadoDoc
        [InlineData("O", "S")] // cancelar por Cancelado
        public async Task Actualizar_Cancelar_ValidaConAccionCancelar_AntesDeGuardar(string estadoDoc, string cancelado)
        {
            ConPedidoGuardado();

            Assert.True(await _domain.ActualizarAsync(1, new PedidoCompra { EstadoDoc = estadoDoc, Cancelado = cancelado, CodigoSn = "PR-1" }));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(TipoDocumentoCompra.PedidoCompra, 1, "cancelar el documento"), Times.Once);
            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Once);
            Assert.Equal(new[] { "validar:11:1:cancelar el documento", "guardar", "notificar:11:1" }, _orden);
        }

        [Fact]
        public async Task Actualizar_CambiaProveedor_ValidaConAccionCambiarProveedor_AntesDeGuardar()
        {
            ConPedidoGuardado();

            Assert.True(await _domain.ActualizarAsync(1, new PedidoCompra { EstadoDoc = "O", Cancelado = "N", CodigoSn = "PR-2" }));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(TipoDocumentoCompra.PedidoCompra, 1, "cambiar el proveedor"), Times.Once);
            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Once);
            Assert.Equal(new[] { "validar:11:1:cambiar el proveedor", "guardar", "notificar:11:1" }, _orden);
        }

        [Fact]
        public async Task Actualizar_ProveedorConOtraMayusculas_NoCuentaComoCambio()
        {
            ConPedidoGuardado(codigoSn: "pr-1");

            Assert.True(await _domain.ActualizarAsync(1, new PedidoCompra { EstadoDoc = "O", Cancelado = "N", CodigoSn = "PR-1" }));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_CancelarYCambiarProveedor_ValidaSoloUnaVezComoCancelar()
        {
            ConPedidoGuardado();

            await _domain.ActualizarAsync(1, new PedidoCompra { EstadoDoc = "C", Cancelado = "S", CodigoSn = "PR-2" });

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(TipoDocumentoCompra.PedidoCompra, 1, "cancelar el documento"), Times.Once);
            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Actualizar_EditaOtrosCampos_NoValida()
        {
            ConPedidoGuardado();

            Assert.True(await _domain.ActualizarAsync(1, new PedidoCompra { EstadoDoc = "O", Cancelado = "N", CodigoSn = "PR-1", Comentario = "nota" }));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            Assert.Equal(new[] { "guardar", "notificar:11:1" }, _orden);
        }

        [Theory]
        [InlineData("C", "N")] // ya cancelado por EstadoDoc
        [InlineData("O", "S")] // ya cancelado por Cancelado
        public async Task Actualizar_YaCancelado_QueSeVuelveAGuardarCancelado_NoValida(string estadoExistente, string canceladoExistente)
        {
            ConPedidoGuardado(estadoExistente, canceladoExistente);

            Assert.True(await _domain.ActualizarAsync(1, new PedidoCompra { EstadoDoc = "C", Cancelado = "S", CodigoSn = "PR-1" }));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_PedidoInexistente_ConservaElComportamientoActual_SinValidar()
        {
            _repoPedido.Setup(r => r.ObtenerAsync(1)).ReturnsAsync((PedidoCompra?)null!);

            Assert.True(await _domain.ActualizarAsync(1, new PedidoCompra { Cancelado = "S", CodigoSn = "PR-2" }));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("cancelar")]
        [InlineData("proveedor")]
        public async Task Actualizar_LaValidacionLanza_PropagaElMensaje_YNoGuardaNiLeeParesNiAvisa(string caso)
        {
            ConPedidoGuardado();
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("El Pedido de compra 5 tiene documentos destino activos."));
            var obj = caso == "cancelar"
                ? new PedidoCompra { Cancelado = "S", CodigoSn = "PR-1" }
                : new PedidoCompra { Cancelado = "N", CodigoSn = "PR-2" };

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(1, obj));

            Assert.Equal("El Pedido de compra 5 tiene documentos destino activos.", ex.Message);
            SinAvisosNiCambios();
            Assert.Empty(_orden);
        }

        // ---------------- EliminarAsync ----------------

        [Fact]
        public async Task Eliminar_ValidaPrimero_AntesDeLeerParesYDeBorrar()
        {
            ConPedidoGuardado();

            Assert.True(await _domain.EliminarAsync(1));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(TipoDocumentoCompra.PedidoCompra, 1, "eliminar el documento"), Times.Once);
            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Once);
            Assert.Equal(new[] { "validar:11:1:eliminar el documento", "obtener:11:1", "borrar-linea", "borrar-cabecera", "notificar-pares" }, _orden);
        }

        [Fact]
        public async Task Eliminar_LaValidacionLanza_PropagaElMensaje_YNoBorraNiLeeParesNiAvisa()
        {
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("El Pedido de compra 5 tiene documentos destino activos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(1));

            Assert.Equal("El Pedido de compra 5 tiene documentos destino activos.", ex.Message);
            SinAvisosNiCambios();
            _repoDetalle.Verify(r => r.ObtenerTodoAsync(), Times.Never);
            Assert.Empty(_orden);
        }
    }
}
