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
    /// Integridad del origen (Copiar a en compras): una entrega de compra con facturas destino activas no se cancela. La
    /// validacion va dentro del camino de cancelacion y ANTES de la transaccion (no se revierte inventario ni se avisa si se
    /// rechaza). Editar solo el comentario y eliminar (que ya exige cancelar antes) no validan.
    /// </summary>
    public class EntregaCompraIntegridadTests
    {
        private readonly List<string> _orden = new();

        private readonly Mock<IRepositorioGenerico<EntregaCompra, int>> _repoEntrega = new();
        private readonly Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly Mock<IExistenciaCompromisoService> _compromisos = new();
        private readonly Mock<IDocumentoBaseCompraService> _documentoBase = new();
        private readonly EntregaCompraDomain _domain;

        public EntregaCompraIntegridadTests()
        {
            _domain = new EntregaCompraDomain(_repoEntrega.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object, null, _compromisos.Object, _documentoBase.Object);

            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback<int, int, string>((t, e, a) => _orden.Add($"validar:{t}:{e}:{a}")).Returns(Task.CompletedTask);

            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(async f => { var r = await f(); _orden.Add("tx"); return r; });
            _asiento.Setup(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>())).Callback(() => _orden.Add("revertir")).Returns(Task.CompletedTask);
            _compromisos.Setup(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Callback<int, int>((t, e) => _orden.Add($"notificar:{t}:{e}")).Returns(Task.CompletedTask);

            _repoEntrega.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new EntregaCompra { Entry = 1, EstadoInv = "A", Cancelado = "N", CodigoSn = "PR-1" });
        }

        [Fact]
        public async Task Actualizar_Cancelar_ValidaAntesDeLaTransaccion()
        {
            Assert.True(await _domain.ActualizarAsync(1, new EntregaCompra { Cancelado = "S" }));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(TipoDocumentoCompra.EntregaCompra, 1, "cancelar el documento"), Times.Once);
            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Once);
            Assert.Equal(new[] { "validar:12:1:cancelar el documento", "revertir", "tx", "notificar:12:1" }, _orden);
        }

        [Fact]
        public async Task Actualizar_Cancelar_LaValidacionLanza_PropagaElMensaje_YNoCorreLaTransaccionNiAvisa()
        {
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("La Entrega de compra 7 tiene documentos destino activos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(1, new EntregaCompra { Cancelado = "S" }));

            Assert.Equal("La Entrega de compra 7 tiene documentos destino activos.", ex.Message);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>()), Times.Never);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            Assert.Empty(_orden);
        }

        [Fact]
        public async Task Actualizar_SoloComentario_NoValida()
        {
            Assert.True(await _domain.ActualizarAsync(1, new EntregaCompra { Cancelado = "N", Comentario = "nota" }));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            Assert.Equal(new[] { "tx" }, _orden);
        }

        [Fact]
        public async Task Actualizar_Inexistente_DevuelveFalse_SinValidar()
        {
            _repoEntrega.Setup(r => r.ObtenerAsync(1)).ReturnsAsync((EntregaCompra?)null!);

            Assert.False(await _domain.ActualizarAsync(1, new EntregaCompra { Cancelado = "S" }));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_YaCancelada_LanzaSuMensaje_SinValidar()
        {
            _repoEntrega.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new EntregaCompra { Entry = 1, EstadoInv = "C", Cancelado = "S" });

            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(1, new EntregaCompra { Cancelado = "S" }));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }
    }
}
