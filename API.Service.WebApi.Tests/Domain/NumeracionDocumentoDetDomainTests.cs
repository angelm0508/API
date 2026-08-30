using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class NumeracionDocumentoDetDomainTests
    {
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoMock;
        private readonly NumeracionDocumentoDetDomain _domain;

        public NumeracionDocumentoDetDomainTests()
        {
            _repoMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new NumeracionDocumentoDetDomain(_repoMock.Object);
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "3",
            Serie = 5,
            NombreSerie = "Primario",
            SigNumero = sigNumero,
            FinNumero = finNumero,
            Bloqueado = bloqueado,
            Manual = "N",
            SubTipoDoc = "--",
            TipoSerie = "N",
            IniCadena = "COT-",
            CantDigitos = 4,
            FinCadena = ""
        };

        [Fact]
        public async Task GenerarCodigoAsync_NoIncrementaNiPersisteElConsecutivo()
        {
            var serie = SerieAutogenerada(sigNumero: 1);
            _repoMock.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(serie);

            var codigo = await _domain.GenerarCodigoAsync(5);

            Assert.Equal("COT-0001", codigo);
            Assert.Equal(1, serie.SigNumero);
            _repoMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task GenerarCodigoAsync_LlamadoDosVecesSeguidas_DevuelveElMismoCodigo()
        {
            var serie = SerieAutogenerada(sigNumero: 1);
            _repoMock.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(serie);

            var primero = await _domain.GenerarCodigoAsync(5);
            var segundo = await _domain.GenerarCodigoAsync(5);

            Assert.Equal(primero, segundo);
            Assert.Equal("COT-0001", segundo);
        }

        [Fact]
        public async Task GenerarCodigoAsync_SerieBloqueada_Lanza()
        {
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoMock.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(serie);

            await Assert.ThrowsAsync<Exception>(() => _domain.GenerarCodigoAsync(5));
        }

        [Fact]
        public async Task GenerarCodigoAsync_SerieInexistente_Lanza()
        {
            _repoMock.Setup(r => r.ObtenerAsync(5)).ReturnsAsync((NumeracionDocumentoDet?)null);

            await Assert.ThrowsAsync<Exception>(() => _domain.GenerarCodigoAsync(5));
        }

        [Fact]
        public async Task GenerarCodigoAsync_SinSigNumero_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: null);
            _repoMock.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(serie);

            await Assert.ThrowsAsync<Exception>(() => _domain.GenerarCodigoAsync(5));
        }

        [Fact]
        public async Task GenerarCodigoAsync_NumeracionAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoMock.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(serie);

            await Assert.ThrowsAsync<Exception>(() => _domain.GenerarCodigoAsync(5));
        }

        [Fact]
        public void FormatearCodigo_ArmaElCodigoConPaddingDeCeros()
        {
            var serie = SerieAutogenerada(sigNumero: 7);

            var codigo = NumeracionDocumentoDetDomain.FormatearCodigo(serie);

            Assert.Equal("COT-0007", codigo);
        }
    }
}
