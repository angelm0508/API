using API.Domain.Core;
using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class EntregaDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Entrega, int>> _repoHeader = new();
        private readonly Mock<IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly EntregaDomain _domain;

        private readonly List<MovimientoRequest> _movimientosAsentados = new();

        public EntregaDomainTests()
        {
            _domain = new EntregaDomain(_repoHeader.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object, _documentoBase.Object);
            // El doble del ejecutor corre el Func directo (sin transacción ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoHeader.Setup(r => r.InsertarAsync(It.IsAny<Entrega>()))
                .ReturnsAsync((Entrega c) => { c.Entry = 99; return c; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<EntregaDetalle>())).Returns(Task.CompletedTask);
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((ms, _) => _movimientosAsentados.AddRange(ms))
                .Returns(Task.CompletedTask);
        }

        private static NumeracionDocumentoDet SerieAuto(int? sig = 5, int? fin = null, string bloqueado = "N", string manual = "N") => new()
        {
            CodigoObj = "5", Serie = 4, NombreSerie = "Primario",
            SigNumero = sig, FinNumero = fin, Bloqueado = bloqueado, Manual = manual,
            SubTipoDoc = "--", TipoSerie = "N"
        };

        private static EntregaDetalle Linea(string art, string alm, decimal? cant, decimal? precio) =>
            new() { CodArticulo = art, CodAlmacen = alm, Cantidad = cant, Precio = precio };

        private static EntregaDetalle LineaDe(string art, string alm, decimal? cant, decimal? precio, int? baseTipo, int? baseEntry, int? baseLinea) =>
            new() { CodArticulo = art, CodAlmacen = alm, Cantidad = cant, Precio = precio, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea };
        [Fact]
        public async Task InsertarAsync_ConLineas_NumeraAsientaYMarcaEstadoInv()
        {
            var serie = SerieAuto(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var obj = new Entrega { Serie = 4, FechaDoc = new DateTime(2026, 8, 30) };
            var lineas = new[] { Linea("ART1", "01", 10m, 25m), Linea("ART2", "01", 5m, 30m) };

            var entry = await _domain.InsertarAsync(obj, lineas);

            Assert.Equal(99, entry);
            Assert.Equal("5", obj.TipoObjeto);
            Assert.Equal("A", obj.EstadoInv);
            Assert.Equal(5, obj.NumDoc);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Once);
            Assert.Equal(6, serie.SigNumero); // arrancaba en 5; debe haber avanzado
            // 2 líneas con Entry/NoLinea asignados
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<EntregaDetalle>(l => l.Entry == 99 && l.NoLinea == 1)), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<EntregaDetalle>(l => l.Entry == 99 && l.NoLinea == 2)), Times.Once);
            // 2 MovimientoRequest correctos -- cantidad en negativo (salida de stock)
            Assert.Equal(2, _movimientosAsentados.Count);
            Assert.Equal(("5", 99, 1, "ART1", "01", -10m, 25m), (
                _movimientosAsentados[0].TipoDoc, _movimientosAsentados[0].DocEntry, _movimientosAsentados[0].DocLinea,
                _movimientosAsentados[0].CodArticulo, _movimientosAsentados[0].CodAlmacen,
                _movimientosAsentados[0].Cantidad, _movimientosAsentados[0].PrecioUnitario));
            Assert.Equal(2, _movimientosAsentados[1].DocLinea);
        }

        [Fact]
        public async Task InsertarAsync_ConCanceladoEnviadoPorElCliente_LoIgnora()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Entrega { Serie = 4, Cancelado = "S", FechaCancelado = new DateTime(2020, 1, 1) };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", 10m, 25m) });

            Assert.Equal("N", obj.Cancelado);
            Assert.Null(obj.FechaCancelado);
            Assert.Equal("A", obj.EstadoInv);
        }

        [Fact]
        public async Task InsertarAsync_LineaSinCantidad_NoGeneraMovimiento()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Entrega { Serie = 4 };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", null, 25m), Linea("ART2", "01", 0m, 10m) });

            Assert.Empty(_movimientosAsentados);
        }

        [Fact]
        public async Task InsertarAsync_StockInsuficiente_Propaga()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .ThrowsAsync(new StockInsuficienteException("ART1", "01", 3m, 10m));

            var obj = new Entrega { Serie = 4 };
            var lineas = new[] { Linea("ART1", "01", 10m, 25m) };

            await Assert.ThrowsAsync<StockInsuficienteException>(() => _domain.InsertarAsync(obj, lineas));
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza_YNoInserta()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(bloqueado: "S"));
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new Entrega { Serie = 4 }, new List<EntregaDetalle>()));
            _repoHeader.Verify(r => r.InsertarAsync(It.IsAny<Entrega>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 10, fin: 9));
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new Entrega { Serie = 4 }, new List<EntregaDetalle>()));
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumDoc_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(manual: "S"));
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new Entrega { Serie = 4, NumDoc = 0 }, new List<EntregaDetalle>()));
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new Entrega { Serie = 4 }, new List<EntregaDetalle>()));
        }

        [Fact]
        public async Task ActualizarAsync_Cancelado_S_RevierteYMarcaEstadoInvC()
        {
            var existente = new Entrega { Entry = 7, Cancelado = "N", EstadoInv = "A" };
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(existente);

            var ok = await _domain.ActualizarAsync(7, new Entrega { Cancelado = "S", Comentario = "anulado" });

            Assert.True(ok);
            _asiento.Verify(a => a.RevertirAsync("5", 7), Times.Once);
            Assert.Equal("S", existente.Cancelado);
            Assert.Equal("C", existente.EstadoInv);
            Assert.NotNull(existente.FechaCancelado);
            Assert.Equal("anulado", existente.Comentario);
        }

        [Fact]
        public async Task ActualizarAsync_Cancelado_SinComentario_PreservaElExistente()
        {
            var existente = new Entrega { Entry = 7, Cancelado = "N", EstadoInv = "A", Comentario = "nota original" };
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(existente);

            var ok = await _domain.ActualizarAsync(7, new Entrega { Cancelado = "S" });

            Assert.True(ok);
            Assert.Equal("nota original", existente.Comentario);
            Assert.Equal("S", existente.Cancelado);
            Assert.Equal("C", existente.EstadoInv);
        }

        [Fact]
        public async Task ActualizarAsync_YaCancelado_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Entrega { Entry = 7, Cancelado = "S" });
            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(7, new Entrega { Comentario = "x" }));
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_Inocua_ComentarioNull_LoBorra()
        {
            var existente = new Entrega { Entry = 7, Cancelado = "N", EstadoInv = "A", Comentario = "algo" };
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(existente);

            var ok = await _domain.ActualizarAsync(7, new Entrega { Comentario = null });

            Assert.True(ok);
            Assert.Null(existente.Comentario);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_Inocua_SoloCopiaComentario()
        {
            var existente = new Entrega { Entry = 7, Cancelado = "N", EstadoInv = "A", CodigoSn = "SN-ORIG", MonedaDoc = "GTQ", Comentario = "viejo" };
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(existente);

            await _domain.ActualizarAsync(7, new Entrega { Comentario = "nuevo", CodigoSn = "SN-HACK", MonedaDoc = "USD" });

            Assert.Equal("nuevo", existente.Comentario);
            Assert.Equal("SN-ORIG", existente.CodigoSn);
            Assert.Equal("GTQ", existente.MonedaDoc);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_AsentadoNoCancelado_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Entrega { Entry = 7, EstadoInv = "A", Cancelado = "N" });
            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(7));
            _repoHeader.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_Cancelado_BorraLineasYEncabezado()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Entrega { Entry = 7, EstadoInv = "C", Cancelado = "S" });
            _repoDetalle.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new List<EntregaDetalle>
                {
                    new() { Entry = 7, NoLinea = 1 },
                    new() { Entry = 7, NoLinea = 2 },
                }.AsAsyncQueryable()); // helper de TestHelpers: soporta ToListAsync
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int, int)>())).ReturnsAsync(true);
            _repoHeader.Setup(r => r.EliminarAsync(7)).ReturnsAsync(true);

            var ok = await _domain.EliminarAsync(7);

            Assert.True(ok);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 7 && k.NoLinea == 1)), Times.Once);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 7 && k.NoLinea == 2)), Times.Once);
            _repoHeader.Verify(r => r.EliminarAsync(7), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_LineaConBase_SigueAsentandoInventario()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));

            await _domain.InsertarAsync(new Entrega { Serie = 4 }, new[] { LineaDe("ART1", "01", 10m, 25m, baseTipo: 4, baseEntry: 500, baseLinea: 1) });

            var m = Assert.Single(_movimientosAsentados);
            Assert.Equal(-10m, m.Cantidad);
        }

        [Fact]
        public async Task InsertarAsync_LineasConBase_ValidaConElServicio()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Entrega { Serie = 4, CodigoSn = "CL-1" };

            await _domain.InsertarAsync(obj, new[]
            {
                LineaDe("ART1", "01", 10m, 25m, baseTipo: 3, baseEntry: 500, baseLinea: 2),
                LineaDe("ART2", "01", 1m, 25m, baseTipo: null, baseEntry: null, baseLinea: null),
            });

            _documentoBase.Verify(d => d.ValidarLineasAsync(TipoDocumentoVenta.Entrega, "CL-1",
                It.Is<IReadOnlyList<LineaBaseRequest>>(l => l.Count == 1 && l[0] == new LineaBaseRequest(3, 500, 2, "ART1", 10m))), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SinLineasConBase_NoLlamaAlServicio()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));

            await _domain.InsertarAsync(new Entrega { Serie = 4 }, new[] { Linea("ART1", "01", 10m, 25m) });

            _documentoBase.Verify(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_ValidacionDeBaseFalla_NoNumeraNiGuardaNiAsienta()
        {
            var serie = SerieAuto(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _documentoBase.Setup(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .ThrowsAsync(new Exception("El socio del documento debe ser el del documento base Pedido 3 (CL-1)."));

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new Entrega { Serie = 4 }, new[] { LineaDe("ART1", "01", 10m, 25m, baseTipo: 4, baseEntry: 500, baseLinea: 1) }));

            Assert.Equal("El socio del documento debe ser el del documento base Pedido 3 (CL-1).", ex.Message);
            Assert.Equal(5, serie.SigNumero);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _repoHeader.Verify(r => r.InsertarAsync(It.IsAny<Entrega>()), Times.Never);
            Assert.Empty(_movimientosAsentados);
        }

        [Fact]
        public async Task InsertarAsync_CabeceraSiempreConBaseTipoMenosUno()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Entrega { Serie = 4, BaseTipo = 4, BaseEntry = 77 };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", 1m, 1m) });

            Assert.Equal(-1, obj.BaseTipo);
            Assert.Null(obj.BaseEntry);
        }

        [Fact]
        public async Task ActualizarAsync_Cancelar_ConDerivados_Lanza_YNoRevierteInventario()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Entrega { Entry = 7, Cancelado = "N", EstadoInv = "A" });
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Entrega, 7, "cancelar el documento"))
                .ThrowsAsync(new Exception("No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(7, new Entrega { Cancelado = "S" }));

            Assert.Equal("No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos.", ex.Message);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_EdicionDeComentario_NoValidaElOrigen()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Entrega { Entry = 7, Cancelado = "N", EstadoInv = "A" });

            await _domain.ActualizarAsync(7, new Entrega { Comentario = "x" });

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }
    }
}
