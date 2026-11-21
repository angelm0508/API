using System.Data;
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Existencias;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>
    /// «Copiar a» en compras: la factura de compra valida las líneas con documento base (pedido o entrega de compra) ANTES de
    /// numerar y de abrir la transacción; la cabecera nunca guarda la referencia al documento base; y las líneas que copian
    /// una entrega (BaseTipo 12) no vuelven a asentar inventario (la entrega ya lo hizo).
    /// </summary>
    public class FacturaCompraDocumentoBaseTests
    {
        private readonly List<string> _orden = new();

        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<ISaldoFacturaCompraService> _saldos = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IResolutorVencimiento> _resolutor = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly Mock<IExistenciaCompromisoService> _compromisos = new();
        private readonly Mock<IDocumentoBaseCompraService> _documentoBase = new();
        private readonly NumeracionDocumentoDet _serie;
        private readonly List<List<MovimientoRequest>> _llamadasAsiento = new();
        private FacturaCompra? _insertada;
        private readonly FacturaCompraDomain _domain;

        public FacturaCompraDocumentoBaseTests()
        {
            _domain = new FacturaCompraDomain(_repoFactura.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object,
                _saldos.Object, _repoSocio.Object, _resolutor.Object, null, _compromisos.Object, _documentoBase.Object);

            _serie = new NumeracionDocumentoDet { CodigoObj = "13", Serie = 4, SigNumero = 5, Bloqueado = "N", Manual = "N", SubTipoDoc = "--", TipoSerie = "N", NombreSerie = "P" };
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(_serie);
            _resolutor.Setup(r => r.ResolverAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync((string? _, string? _, DateTime? fecha) => ("CON", (fecha ?? DateTime.Today).Date));
            _repoFactura.Setup(r => r.InsertarAsync(It.IsAny<FacturaCompra>())).ReturnsAsync((FacturaCompra c) => { _insertada = c; c.Entry = 99; return c; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<FacturaCompraDetalle>())).Returns(Task.CompletedTask);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(async f => { _orden.Add("tx"); return await f(); });
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((ms, _) => _llamadasAsiento.Add(ms.ToList())).Returns(Task.CompletedTask);
            _compromisos.Setup(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>())).Returns(Task.CompletedTask);
            _documentoBase.Setup(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .Callback(() => _orden.Add("validar")).Returns(Task.CompletedTask);
        }

        private static FacturaCompraDetalle Linea(int? baseTipo = null, int? baseEntry = null, int? baseLinea = null, string art = "A", decimal cant = 4m) =>
            new() { CodArticulo = art, CodAlmacen = "01", Cantidad = cant, Precio = 10m, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea };

        [Fact]
        public async Task Insertar_ConLineasConBase_ValidaUnaVez_AntesDeNumerarYDeLaTransaccion()
        {
            IReadOnlyList<LineaBaseRequest>? recibidas = null;
            _documentoBase.Setup(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .Callback<int, string?, IReadOnlyList<LineaBaseRequest>>((_, _, l) => { recibidas = l; _orden.Add($"validar:SigNumero={_serie.SigNumero}"); })
                .Returns(Task.CompletedTask);

            await _domain.InsertarAsync(new FacturaCompra { Serie = 4, CodigoSn = "PR-1" },
                new[] { Linea(TipoDocumentoCompra.PedidoCompra, 7, 1, "A", 4m), Linea(), Linea(TipoDocumentoCompra.EntregaCompra, 8, 3, "B", 2m) });

            _documentoBase.Verify(d => d.ValidarLineasAsync(TipoDocumentoCompra.FacturaCompra, "PR-1", It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Once);
            _documentoBase.Verify(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Once);
            Assert.Equal(new[] { "validar:SigNumero=5", "tx" }, _orden);   // la serie aún no avanzó al validar
            Assert.Equal(6, _serie.SigNumero);                             // y sí avanzó después
            Assert.Equal(new[] { new LineaBaseRequest(11, 7, 1, "A", 4m), new LineaBaseRequest(12, 8, 3, "B", 2m) }, recibidas);
        }

        [Fact]
        public async Task Insertar_ValidacionRechaza_PropagaElMensaje_SinTransaccionNiConsecutivoNiAvisos()
        {
            _documentoBase.Setup(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .ThrowsAsync(new Exception("La línea ya fue copiada."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new FacturaCompra { Serie = 4, CodigoSn = "PR-1" },
                new[] { Linea(TipoDocumentoCompra.PedidoCompra, 7, 1) }));

            Assert.Equal("La línea ya fue copiada.", ex.Message);
            Assert.Empty(_orden);                                   // no se abrió la transacción
            Assert.Equal(5, _serie.SigNumero);                      // no consumió consecutivo
            _repoFactura.Verify(r => r.InsertarAsync(It.IsAny<FacturaCompra>()), Times.Never);
            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()), Times.Never);
            _compromisos.Verify(c => c.NotificarDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _compromisos.Verify(c => c.NotificarParesAsync(It.IsAny<IReadOnlyCollection<ParExistencia>>()), Times.Never);
            _compromisos.Verify(c => c.ObtenerParesAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Insertar_SinLineasConBase_NoLlamaAlServicioDeDocumentoBase()
        {
            await _domain.InsertarAsync(new FacturaCompra { Serie = 4, CodigoSn = "PR-1" },
                new[] { Linea(-1), Linea(null), Linea(0) });

            _documentoBase.Verify(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Never);
            Assert.Equal(new[] { "tx" }, _orden);
        }

        [Fact]
        public async Task Insertar_LaCabeceraNoGuardaLaReferenciaAlDocumentoBase()
        {
            await _domain.InsertarAsync(new FacturaCompra { Serie = 4, CodigoSn = "PR-1", BaseTipo = 11, BaseEntry = 5 },
                new[] { Linea(TipoDocumentoCompra.PedidoCompra, 5, 1) });

            Assert.NotNull(_insertada);
            Assert.Equal(-1, _insertada!.BaseTipo);
            Assert.Null(_insertada.BaseEntry);
        }

        [Fact]
        public async Task Insertar_LasLineasDeEntregaBase_NoAsientanInventario_LasDemasSi()
        {
            // NoLinea se asigna en orden: 1 = base 12 (no asienta), 2 = base 11, 3 = base -1, 4 = sin base.
            await _domain.InsertarAsync(new FacturaCompra { Serie = 4, CodigoSn = "PR-1" },
                new[]
                {
                    Linea(TipoDocumentoCompra.EntregaCompra, 8, 1),
                    Linea(TipoDocumentoCompra.PedidoCompra, 7, 1),
                    Linea(-1),
                    Linea(null)
                });

            var llamada = Assert.Single(_llamadasAsiento);
            Assert.Equal(new[] { 2, 3, 4 }, llamada.Select(m => m.DocLinea).ToArray());
        }

        [Fact]
        public async Task Insertar_TodasLasLineasDeEntregaBase_AsientaConListaVacia_ComoHoy()
        {
            // Comportamiento existente (sin cambios): la factura siempre llama a AsentarAsync, aunque la lista de
            // movimientos quede vacía (p. ej. todas las líneas sin cantidad); el servicio de inventario no hace nada con ella.
            await _domain.InsertarAsync(new FacturaCompra { Serie = 4, CodigoSn = "PR-1" },
                new[] { Linea(TipoDocumentoCompra.EntregaCompra, 8, 1), Linea(TipoDocumentoCompra.EntregaCompra, 8, 2) });

            var llamada = Assert.Single(_llamadasAsiento);
            Assert.Empty(llamada);
        }
    }
}
