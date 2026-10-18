using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PedidoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Pedido, int>> _repoPedidoMock;
        private readonly Mock<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>> _repoDetalleMock;
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracionMock;
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly PedidoDomain _domain;

        public PedidoDomainTests()
        {
            _repoPedidoMock = new Mock<IRepositorioGenerico<Pedido, int>>();
            _repoDetalleMock = new Mock<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new PedidoDomain(_repoPedidoMock.Object, _repoDetalleMock.Object, _repoNumeracionMock.Object, _tx.Object, _documentoBase.Object);
            // El doble del ejecutor corre el Func directo (sin transacción ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _repoDetalleMock.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<PedidoDetalle>())).Returns(Task.CompletedTask);
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "4",
            Serie = 4,
            NombreSerie = "Primario",
            SigNumero = sigNumero,
            FinNumero = finNumero,
            Bloqueado = bloqueado,
            Manual = "N",
            SubTipoDoc = "--",
            TipoSerie = "N"
        };

        [Fact]
        public async Task InsertarAsync_SerieAutogenerada_AsignaSigNumeroYLoIncrementa()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoPedidoMock.Setup(r => r.InsertarAsync(It.IsAny<Pedido>()))
                .ReturnsAsync((Pedido c) => { c.Entry = 99; return c; });

            var obj = new Pedido { Serie = 4, NumDoc = 0, TipoObjeto = "algo-que-el-cliente-mando" };
            var entry = await _domain.InsertarAsync(obj);

            Assert.Equal(99, entry);
            Assert.Equal(5, obj.NumDoc);
            Assert.Equal("4", obj.TipoObjeto);
            Assert.Equal(6, serie.SigNumero);
            _repoNumeracionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_RespetaNumDocDelCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoPedidoMock.Setup(r => r.InsertarAsync(It.IsAny<Pedido>()))
                .ReturnsAsync((Pedido c) => { c.Entry = 1; return c; });

            var obj = new Pedido { Serie = 4, NumDoc = 12345 };
            await _domain.InsertarAsync(obj);

            Assert.Equal(12345, obj.NumDoc);
            Assert.Equal(5, serie.SigNumero);
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumDoc_Lanza()
        {
            var serie = SerieAutogenerada();
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Pedido { Serie = 4, NumDoc = 0 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoPedidoMock.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Pedido { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoPedidoMock.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Pedido { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoPedidoMock.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            var obj = new Pedido { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoPedidoMock.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_FuerzaTipoObjetoACuatro()
        {
            _repoPedidoMock.Setup(r => r.ActualizarAsync(1, It.IsAny<Pedido>())).ReturnsAsync(true);

            var obj = new Pedido { TipoObjeto = "otro-valor" };
            var resultado = await _domain.ActualizarAsync(1, obj);

            Assert.True(resultado);
            Assert.Equal("4", obj.TipoObjeto);
        }

        private static PedidoDetalle LineaPedido(string art, decimal? cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            new() { CodArticulo = art, Cantidad = cant, Precio = 10m, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea };

        [Fact]
        public async Task InsertarConLineas_NumeraGuardaCabeceraYLineasEnUnaTransaccion()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoPedidoMock.Setup(r => r.InsertarAsync(It.IsAny<Pedido>())).ReturnsAsync((Pedido c) => { c.Entry = 99; return c; });
            var obj = new Pedido { Serie = 4, TipoObjeto = "x", BaseTipo = 3, BaseEntry = 8 };

            var entry = await _domain.InsertarAsync(obj, new[] { LineaPedido("ART1", 2m), LineaPedido("ART2", 3m) });

            Assert.Equal(99, entry);
            Assert.Equal((5, "4", -1), (obj.NumDoc, obj.TipoObjeto, obj.BaseTipo));
            Assert.Null(obj.BaseEntry);
            Assert.Equal(6, serie.SigNumero);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Once);
            _repoDetalleMock.Verify(r => r.AgregarSinGuardarAsync(It.Is<PedidoDetalle>(l => l.Entry == 99 && l.NoLinea == 1 && l.CodArticulo == "ART1")), Times.Once);
            _repoDetalleMock.Verify(r => r.AgregarSinGuardarAsync(It.Is<PedidoDetalle>(l => l.Entry == 99 && l.NoLinea == 2 && l.CodArticulo == "ART2")), Times.Once);
        }

        [Fact]
        public async Task InsertarConLineas_LineasConBase_ValidaConElServicioAntesDeNumerar()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoPedidoMock.Setup(r => r.InsertarAsync(It.IsAny<Pedido>())).ReturnsAsync((Pedido c) => { c.Entry = 99; return c; });
            var obj = new Pedido { Serie = 4, CodigoSn = "CL-1" };

            await _domain.InsertarAsync(obj, new[] { LineaPedido("ART1", 4m, baseTipo: 3, baseEntry: 12, baseLinea: 1), LineaPedido("ART2", 1m) });

            _documentoBase.Verify(d => d.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1",
                It.Is<IReadOnlyList<LineaBaseRequest>>(l => l.Count == 1 && l[0] == new LineaBaseRequest(3, 12, 1, "ART1", 4m))), Times.Once);
        }

        [Fact]
        public async Task InsertarConLineas_ValidacionFalla_NoNumeraNiGuarda()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _documentoBase.Setup(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .ThrowsAsync(new Exception("El documento base Cotización 12 está cancelado."));

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new Pedido { Serie = 4 }, new[] { LineaPedido("ART1", 1m, baseTipo: 3, baseEntry: 12, baseLinea: 1) }));

            Assert.Equal("El documento base Cotización 12 está cancelado.", ex.Message);
            Assert.Equal(5, serie.SigNumero);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _repoPedidoMock.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task InsertarConLineas_SerieBloqueada_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAutogenerada(bloqueado: "S"));

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new Pedido { Serie = 4 }, new[] { LineaPedido("ART1", 1m) }));
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
        }

        [Fact]
        public async Task InsertarSinLineas_CaminoAntiguo_NoUsaTransaccionNiServicio()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAutogenerada(sigNumero: 5));
            _repoPedidoMock.Setup(r => r.InsertarAsync(It.IsAny<Pedido>())).ReturnsAsync((Pedido c) => { c.Entry = 7; return c; });

            await _domain.InsertarAsync(new Pedido { Serie = 4 });

            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _documentoBase.Verify(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Never);
        }
    }
}
