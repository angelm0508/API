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
    /// Integridad del origen (Copiar a en compras): una linea de pedido de compra con destinos activos no se modifica ni se
    /// elimina; agregar una linea nueva sigue permitido. La validacion va PRIMERO: antes de resolver unidades, de leer los pares
    /// de Comprometido/Pedido y de guardar; un rechazo no deja rastro.
    /// </summary>
    public class PedidoCompraDetalleIntegridadTests
    {
        private readonly List<string> _orden = new();

        private readonly Mock<IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IUnidadMedidaService> _unidades = new();
        private readonly Mock<IExistenciaCompromisoService> _compromisos = new();
        private readonly Mock<IDocumentoBaseCompraService> _documentoBase = new();
        private readonly PedidoCompraDetalleDomain _domain;

        private static readonly IReadOnlyList<ParExistencia> Pares = new List<ParExistencia> { new("A", "01") };

        public PedidoCompraDetalleIntegridadTests()
        {
            _domain = new PedidoCompraDetalleDomain(_repoDetalle.Object, _unidades.Object, _compromisos.Object, _documentoBase.Object);

            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback<int, int, int, string>((t, e, l, a) => _orden.Add($"validar:{t}:{e}:{l}:{a}")).Returns(Task.CompletedTask);

            _compromisos.Setup(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Callback<int, int>((t, e) => _orden.Add($"notificar:{t}:{e}")).Returns(Task.CompletedTask);
            _compromisos.Setup(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()))
                .Callback<IReadOnlyCollection<ParExistencia>>(_ => _orden.Add("notificar-pares")).Returns(Task.CompletedTask);
            _compromisos.Setup(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Callback<int, int>((t, e) => _orden.Add($"obtener:{t}:{e}")).ReturnsAsync(Pares);

            _repoDetalle.Setup(r => r.ActualizarAsync(It.IsAny<(int Entry, int NoLinea)>(), It.IsAny<PedidoCompraDetalle>()))
                .Callback(() => _orden.Add("guardar")).ReturnsAsync(true);
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>()))
                .Callback(() => _orden.Add("borrar-linea")).ReturnsAsync(true);
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle>().AsAsyncQueryable());
            _repoDetalle.Setup(r => r.InsertarAsync(It.IsAny<PedidoCompraDetalle>())).Callback(() => _orden.Add("insertar")).ReturnsAsync((PedidoCompraDetalle l) => l);
        }

        private void SinResolverUnidadesNiLeerParesNiGuardarNiAvisar()
        {
            _unidades.VerifyNoOtherCalls();
            _repoDetalle.Verify(r => r.ActualizarAsync(It.IsAny<(int Entry, int NoLinea)>(), It.IsAny<PedidoCompraDetalle>()), Times.Never);
            _repoDetalle.Verify(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>()), Times.Never);
            _compromisos.Verify(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
        }

        // ---------------- ActualizarAsync ----------------

        [Fact]
        public async Task Actualizar_ValidaPrimero_AntesDeLeerParesYGuardar()
        {
            Assert.True(await _domain.ActualizarAsync(5, 2, new PedidoCompraDetalle { Entry = 5, NoLinea = 2 }));

            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(TipoDocumentoCompra.PedidoCompra, 5, 2, "modificar"), Times.Once);
            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Once);
            Assert.Equal(new[] { "validar:11:5:2:modificar", "obtener:11:5", "guardar", "notificar-pares", "notificar:11:5" }, _orden);
        }

        [Fact]
        public async Task Actualizar_LaValidacionLanza_PropagaElMensaje_YNoResuelveUnidadesNiLeeParesNiGuardaNiAvisa()
        {
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("La linea 2 del Pedido de compra 5 ya tiene documentos destino."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, 2, new PedidoCompraDetalle { Entry = 5, NoLinea = 2, CodArticulo = "A", Cantidad = 3m }));

            Assert.Equal("La linea 2 del Pedido de compra 5 ya tiene documentos destino.", ex.Message);
            SinResolverUnidadesNiLeerParesNiGuardarNiAvisar();
            Assert.Empty(_orden);
        }

        // ---------------- EliminarAsync ----------------

        [Fact]
        public async Task Eliminar_ValidaPrimero_AntesDeLeerParesYBorrar()
        {
            Assert.True(await _domain.EliminarAsync(5, 2));

            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(TipoDocumentoCompra.PedidoCompra, 5, 2, "eliminar"), Times.Once);
            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Once);
            Assert.Equal(new[] { "validar:11:5:2:eliminar", "obtener:11:5", "borrar-linea", "notificar-pares" }, _orden);
        }

        [Fact]
        public async Task Eliminar_LaValidacionLanza_PropagaElMensaje_YNoLeeParesNiBorraNiAvisa()
        {
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("La linea 2 del Pedido de compra 5 ya tiene documentos destino."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(5, 2));

            Assert.Equal("La linea 2 del Pedido de compra 5 ya tiene documentos destino.", ex.Message);
            SinResolverUnidadesNiLeerParesNiGuardarNiAvisar();
            Assert.Empty(_orden);
        }

        // ---------------- InsertarAsync ----------------

        [Fact]
        public async Task Insertar_LineaNueva_NoValida()
        {
            var noLinea = await _domain.InsertarAsync(new PedidoCompraDetalle { Entry = 5 });

            Assert.Equal(1, noLinea);
            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            Assert.Equal(new[] { "insertar", "notificar:11:5" }, _orden);
        }
    }
}
