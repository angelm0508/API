using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PedidoCompraDomainTests
    {
        private readonly Mock<IRepositorioGenerico<PedidoCompra, int>> _repoPedidoCompraMock;
        private readonly Mock<IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)>> _repoDetalleMock;
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracionMock;
        private readonly Mock<IRepositorioGenerico<EntregaCompra, int>> _repoEntregaCompraMock;
        private readonly Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>> _repoEntregaCompraDetalleMock;
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoFacturaCompraMock;
        private readonly Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>> _repoFacturaCompraDetalleMock;
        private readonly PedidoCompraDomain _domain;

        public PedidoCompraDomainTests()
        {
            _repoPedidoCompraMock = new Mock<IRepositorioGenerico<PedidoCompra, int>>();
            _repoDetalleMock = new Mock<IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _repoEntregaCompraMock = new Mock<IRepositorioGenerico<EntregaCompra, int>>();
            _repoEntregaCompraDetalleMock = new Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>>();
            _repoFacturaCompraMock = new Mock<IRepositorioGenerico<FacturaCompra, int>>();
            _repoFacturaCompraDetalleMock = new Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>>();
            _domain = new PedidoCompraDomain(
                _repoPedidoCompraMock.Object, _repoDetalleMock.Object, _repoNumeracionMock.Object,
                _repoEntregaCompraMock.Object, _repoEntregaCompraDetalleMock.Object,
                _repoFacturaCompraMock.Object, _repoFacturaCompraDetalleMock.Object);
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "11",
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
            _repoPedidoCompraMock.Setup(r => r.InsertarAsync(It.IsAny<PedidoCompra>()))
                .ReturnsAsync((PedidoCompra c) => { c.Entry = 99; return c; });

            var obj = new PedidoCompra { Serie = 4, NumDoc = 0, TipoObjeto = "algo-que-el-cliente-mando" };
            var entry = await _domain.InsertarAsync(obj);

            Assert.Equal(99, entry);
            Assert.Equal(5, obj.NumDoc);
            Assert.Equal("11", obj.TipoObjeto);
            Assert.Equal(6, serie.SigNumero);
            _repoNumeracionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_RespetaNumDocDelCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoPedidoCompraMock.Setup(r => r.InsertarAsync(It.IsAny<PedidoCompra>()))
                .ReturnsAsync((PedidoCompra c) => { c.Entry = 1; return c; });

            var obj = new PedidoCompra { Serie = 4, NumDoc = 12345 };
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

            var obj = new PedidoCompra { Serie = 4, NumDoc = 0 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoPedidoCompraMock.Verify(r => r.InsertarAsync(It.IsAny<PedidoCompra>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new PedidoCompra { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoPedidoCompraMock.Verify(r => r.InsertarAsync(It.IsAny<PedidoCompra>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new PedidoCompra { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoPedidoCompraMock.Verify(r => r.InsertarAsync(It.IsAny<PedidoCompra>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            var obj = new PedidoCompra { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoPedidoCompraMock.Verify(r => r.InsertarAsync(It.IsAny<PedidoCompra>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_FuerzaTipoObjeto()
        {
            _repoPedidoCompraMock.Setup(r => r.ActualizarAsync(1, It.IsAny<PedidoCompra>())).ReturnsAsync(true);

            var obj = new PedidoCompra { TipoObjeto = "otro-valor" };
            var resultado = await _domain.ActualizarAsync(1, obj);

            Assert.True(resultado);
            Assert.Equal("11", obj.TipoObjeto);
        }

        [Fact]
        public async Task ObtenerAbiertosAsync_SinConsumo_DevuelveCantidadTotalComoAbierta()
        {
            var pedido = new PedidoCompra { Entry = 1, NumDoc = 100, Serie = 4, CodigoSn = "PV0001", Cancelado = "N", FechaDoc = new DateTime(2026, 9, 1) };
            var linea = new PedidoCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Descripcion = "Articulo 1", Cantidad = 10m, Precio = 25m, CodAlmacen = "01", CodigoImpuesto = "IVA" };
            _repoPedidoCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { pedido }.AsAsyncQueryable());
            _repoDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { linea }.AsAsyncQueryable());
            _repoEntregaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra>().AsAsyncQueryable());
            _repoEntregaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle>().AsAsyncQueryable());
            _repoFacturaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
            _repoFacturaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

            var resultado = (await _domain.ObtenerAbiertosAsync("PV0001")).ToList();

            Assert.Single(resultado);
            Assert.Equal(1, resultado[0].Entry);
            Assert.Single(resultado[0].Lineas);
            Assert.Equal(10m, resultado[0].Lineas[0].CantidadAbierta);
        }

        [Fact]
        public async Task ObtenerAbiertosAsync_ConEntregaYFacturaDirectaActivas_RestaAmbasDelMismoPool()
        {
            var pedido = new PedidoCompra { Entry = 1, NumDoc = 100, Serie = 4, CodigoSn = "PV0001", Cancelado = "N" };
            var linea = new PedidoCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 10m };
            var entregaHeader = new EntregaCompra { Entry = 50, Cancelado = "N" };
            var entregaLinea = new EntregaCompraDetalle { Entry = 50, NoLinea = 1, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1, Cantidad = 4m };
            var facturaHeader = new FacturaCompra { Entry = 60, Cancelado = "N" };
            var facturaLinea = new FacturaCompraDetalle { Entry = 60, NoLinea = 1, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1, Cantidad = 3m };
            _repoPedidoCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { pedido }.AsAsyncQueryable());
            _repoDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { linea }.AsAsyncQueryable());
            _repoEntregaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { entregaHeader }.AsAsyncQueryable());
            _repoEntregaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { entregaLinea }.AsAsyncQueryable());
            _repoFacturaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra> { facturaHeader }.AsAsyncQueryable());
            _repoFacturaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle> { facturaLinea }.AsAsyncQueryable());

            var resultado = (await _domain.ObtenerAbiertosAsync("PV0001")).ToList();

            Assert.Equal(3m, resultado[0].Lineas[0].CantidadAbierta); // 10 - 4 (entrega) - 3 (factura directa)
        }

        [Fact]
        public async Task ObtenerAbiertosAsync_EntregaCancelada_SeIgnoraElConsumo()
        {
            var pedido = new PedidoCompra { Entry = 1, NumDoc = 100, Serie = 4, CodigoSn = "PV0001", Cancelado = "N" };
            var linea = new PedidoCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 10m };
            var entregaHeader = new EntregaCompra { Entry = 50, Cancelado = "S" };
            var entregaLinea = new EntregaCompraDetalle { Entry = 50, NoLinea = 1, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1, Cantidad = 4m };
            _repoPedidoCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { pedido }.AsAsyncQueryable());
            _repoDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { linea }.AsAsyncQueryable());
            _repoEntregaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { entregaHeader }.AsAsyncQueryable());
            _repoEntregaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { entregaLinea }.AsAsyncQueryable());
            _repoFacturaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
            _repoFacturaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

            var resultado = (await _domain.ObtenerAbiertosAsync("PV0001")).ToList();

            Assert.Equal(10m, resultado[0].Lineas[0].CantidadAbierta);
        }

        [Fact]
        public async Task ObtenerAbiertosAsync_LineaSinCantidadAbierta_NoAparece()
        {
            var pedido = new PedidoCompra { Entry = 1, NumDoc = 100, Serie = 4, CodigoSn = "PV0001", Cancelado = "N" };
            var linea = new PedidoCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 10m };
            var entregaHeader = new EntregaCompra { Entry = 50, Cancelado = "N" };
            var entregaLinea = new EntregaCompraDetalle { Entry = 50, NoLinea = 1, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1, Cantidad = 10m };
            _repoPedidoCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { pedido }.AsAsyncQueryable());
            _repoDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { linea }.AsAsyncQueryable());
            _repoEntregaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { entregaHeader }.AsAsyncQueryable());
            _repoEntregaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { entregaLinea }.AsAsyncQueryable());
            _repoFacturaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
            _repoFacturaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

            var resultado = (await _domain.ObtenerAbiertosAsync("PV0001")).ToList();

            Assert.Empty(resultado);
        }

        [Fact]
        public async Task ObtenerAbiertosAsync_PedidoDeOtroSocio_NoAparece()
        {
            var pedido = new PedidoCompra { Entry = 1, NumDoc = 100, Serie = 4, CodigoSn = "PV0002", Cancelado = "N" };
            var linea = new PedidoCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 10m };
            _repoPedidoCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { pedido }.AsAsyncQueryable());
            _repoDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { linea }.AsAsyncQueryable());
            _repoEntregaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra>().AsAsyncQueryable());
            _repoEntregaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle>().AsAsyncQueryable());
            _repoFacturaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
            _repoFacturaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

            var resultado = await _domain.ObtenerAbiertosAsync("PV0001");

            Assert.Empty(resultado);
        }

        [Fact]
        public async Task ObtenerAbiertosAsync_PedidoCancelado_NoAparece()
        {
            var pedido = new PedidoCompra { Entry = 1, NumDoc = 100, Serie = 4, CodigoSn = "PV0001", Cancelado = "S" };
            var linea = new PedidoCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 10m };
            _repoPedidoCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { pedido }.AsAsyncQueryable());
            _repoDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { linea }.AsAsyncQueryable());
            _repoEntregaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra>().AsAsyncQueryable());
            _repoEntregaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle>().AsAsyncQueryable());
            _repoFacturaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
            _repoFacturaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

            var resultado = await _domain.ObtenerAbiertosAsync("PV0001");

            Assert.Empty(resultado);
        }
    }
}
