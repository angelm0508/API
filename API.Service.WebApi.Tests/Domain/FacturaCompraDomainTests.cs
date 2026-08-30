using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class FacturaCompraDomainTests
    {
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoFacturaCompraMock;
        private readonly Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>> _repoDetalleMock;
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracionMock;
        private readonly FacturaCompraDomain _domain;

        public FacturaCompraDomainTests()
        {
            _repoFacturaCompraMock = new Mock<IRepositorioGenerico<FacturaCompra, int>>();
            _repoDetalleMock = new Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new FacturaCompraDomain(_repoFacturaCompraMock.Object, _repoDetalleMock.Object, _repoNumeracionMock.Object);
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "13",
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
            _repoFacturaCompraMock.Setup(r => r.InsertarAsync(It.IsAny<FacturaCompra>()))
                .ReturnsAsync((FacturaCompra c) => { c.Entry = 99; return c; });

            var obj = new FacturaCompra { Serie = 4, NumDoc = 0, TipoObjeto = "algo-que-el-cliente-mando" };
            var entry = await _domain.InsertarAsync(obj);

            Assert.Equal(99, entry);
            Assert.Equal(5, obj.NumDoc);
            Assert.Equal("13", obj.TipoObjeto);
            Assert.Equal(6, serie.SigNumero);
            _repoNumeracionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_RespetaNumDocDelCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoFacturaCompraMock.Setup(r => r.InsertarAsync(It.IsAny<FacturaCompra>()))
                .ReturnsAsync((FacturaCompra c) => { c.Entry = 1; return c; });

            var obj = new FacturaCompra { Serie = 4, NumDoc = 12345 };
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

            var obj = new FacturaCompra { Serie = 4, NumDoc = 0 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoFacturaCompraMock.Verify(r => r.InsertarAsync(It.IsAny<FacturaCompra>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new FacturaCompra { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoFacturaCompraMock.Verify(r => r.InsertarAsync(It.IsAny<FacturaCompra>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new FacturaCompra { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoFacturaCompraMock.Verify(r => r.InsertarAsync(It.IsAny<FacturaCompra>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            var obj = new FacturaCompra { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoFacturaCompraMock.Verify(r => r.InsertarAsync(It.IsAny<FacturaCompra>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_FuerzaTipoObjetoATrece()
        {
            _repoFacturaCompraMock.Setup(r => r.ActualizarAsync(1, It.IsAny<FacturaCompra>())).ReturnsAsync(true);

            var obj = new FacturaCompra { TipoObjeto = "otro-valor" };
            var resultado = await _domain.ActualizarAsync(1, obj);

            Assert.True(resultado);
            Assert.Equal("13", obj.TipoObjeto);
        }
    }
}
