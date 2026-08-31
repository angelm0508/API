using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class EntregaCompraDomainTests
    {
        private readonly Mock<IRepositorioGenerico<EntregaCompra, int>> _repoHeader = new();
        private readonly Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly EntregaCompraDomain _domain;

        private readonly List<MovimientoRequest> _movimientosAsentados = new();

        public EntregaCompraDomainTests()
        {
            _domain = new EntregaCompraDomain(_repoHeader.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object);
            // El doble del ejecutor corre el Func directo (sin transacción ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoHeader.Setup(r => r.InsertarAsync(It.IsAny<EntregaCompra>()))
                .ReturnsAsync((EntregaCompra c) => { c.Entry = 99; return c; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<EntregaCompraDetalle>())).Returns(Task.CompletedTask);
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((ms, _) => _movimientosAsentados.AddRange(ms))
                .Returns(Task.CompletedTask);
        }

        private static NumeracionDocumentoDet SerieAuto(int? sig = 5, int? fin = null, string bloqueado = "N", string manual = "N") => new()
        {
            CodigoObj = "12", Serie = 4, NombreSerie = "Primario",
            SigNumero = sig, FinNumero = fin, Bloqueado = bloqueado, Manual = manual,
            SubTipoDoc = "--", TipoSerie = "N"
        };

        private static EntregaCompraDetalle Linea(string art, string alm, decimal? cant, decimal? precio) =>
            new() { CodArticulo = art, CodAlmacen = alm, Cantidad = cant, Precio = precio };

        [Fact]
        public async Task InsertarAsync_ConLineas_NumeraAsientaYMarcaEstadoInv()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new EntregaCompra { Serie = 4, FechaDoc = new DateTime(2026, 8, 30) };
            var lineas = new[] { Linea("ART1", "01", 10m, 25m), Linea("ART2", "01", 5m, 30m) };

            var entry = await _domain.InsertarAsync(obj, lineas);

            Assert.Equal(99, entry);
            Assert.Equal("12", obj.TipoObjeto);
            Assert.Equal("A", obj.EstadoInv);
            Assert.Equal(5, obj.NumDoc);
            // 2 líneas con Entry/NoLinea asignados
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<EntregaCompraDetalle>(l => l.Entry == 99 && l.NoLinea == 1)), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<EntregaCompraDetalle>(l => l.Entry == 99 && l.NoLinea == 2)), Times.Once);
            // 2 MovimientoRequest correctos
            Assert.Equal(2, _movimientosAsentados.Count);
            Assert.Equal(("12", 99, 1, "ART1", "01", 10m, 25m), (
                _movimientosAsentados[0].TipoDoc, _movimientosAsentados[0].DocEntry, _movimientosAsentados[0].DocLinea,
                _movimientosAsentados[0].CodArticulo, _movimientosAsentados[0].CodAlmacen,
                _movimientosAsentados[0].Cantidad, _movimientosAsentados[0].PrecioUnitario));
            Assert.Equal(2, _movimientosAsentados[1].DocLinea);
        }

        [Fact]
        public async Task InsertarAsync_LineaSinCantidad_NoGeneraMovimiento()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new EntregaCompra { Serie = 4 };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", null, 25m), Linea("ART2", "01", 0m, 10m) });

            Assert.Empty(_movimientosAsentados);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza_YNoInserta()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(bloqueado: "S"));
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompra { Serie = 4 }, new List<EntregaCompraDetalle>()));
            _repoHeader.Verify(r => r.InsertarAsync(It.IsAny<EntregaCompra>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 10, fin: 9));
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompra { Serie = 4 }, new List<EntregaCompraDetalle>()));
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumDoc_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(manual: "S"));
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompra { Serie = 4, NumDoc = 0 }, new List<EntregaCompraDetalle>()));
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompra { Serie = 4 }, new List<EntregaCompraDetalle>()));
        }

        [Fact]
        public async Task ActualizarAsync_Cancelado_S_RevierteYMarcaEstadoInvC()
        {
            var existente = new EntregaCompra { Entry = 7, Cancelado = "N", EstadoInv = "A" };
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(existente);

            var ok = await _domain.ActualizarAsync(7, new EntregaCompra { Cancelado = "S", Comentario = "anulado" });

            Assert.True(ok);
            _asiento.Verify(a => a.RevertirAsync("12", 7), Times.Once);
            Assert.Equal("S", existente.Cancelado);
            Assert.Equal("C", existente.EstadoInv);
            Assert.NotNull(existente.FechaCancelado);
            Assert.Equal("anulado", existente.Comentario);
        }

        [Fact]
        public async Task ActualizarAsync_YaCancelado_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new EntregaCompra { Entry = 7, Cancelado = "S" });
            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(7, new EntregaCompra { Comentario = "x" }));
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_Inocua_SoloCopiaComentario()
        {
            var existente = new EntregaCompra { Entry = 7, Cancelado = "N", EstadoInv = "A", CodigoSn = "SN-ORIG", MonedaDoc = "GTQ", Comentario = "viejo" };
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(existente);

            await _domain.ActualizarAsync(7, new EntregaCompra { Comentario = "nuevo", CodigoSn = "SN-HACK", MonedaDoc = "USD" });

            Assert.Equal("nuevo", existente.Comentario);
            Assert.Equal("SN-ORIG", existente.CodigoSn);
            Assert.Equal("GTQ", existente.MonedaDoc);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_AsentadoNoCancelado_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new EntregaCompra { Entry = 7, EstadoInv = "A", Cancelado = "N" });
            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(7));
            _repoHeader.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_Cancelado_BorraLineasYEncabezado()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new EntregaCompra { Entry = 7, EstadoInv = "C", Cancelado = "S" });
            _repoDetalle.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new List<EntregaCompraDetalle>().AsAsyncQueryable()); // helper de TestHelpers: soporta ToListAsync
            _repoHeader.Setup(r => r.EliminarAsync(7)).ReturnsAsync(true);

            var ok = await _domain.EliminarAsync(7);

            Assert.True(ok);
            _repoHeader.Verify(r => r.EliminarAsync(7), Times.Once);
        }
    }
}
