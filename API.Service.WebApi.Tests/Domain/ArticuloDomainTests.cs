using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ArticuloDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArticuloMock;
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracionMock;
        private readonly ArticuloDomain _domain;

        public ArticuloDomainTests()
        {
            _repoArticuloMock = new Mock<IRepositorioGenerico<Articulo, string>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new ArticuloDomain(_repoArticuloMock.Object, _repoNumeracionMock.Object);

            _repoArticuloMock.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new List<Articulo>().AsAsyncQueryable());
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "2",
            Serie = 7,
            NombreSerie = "Primario",
            SigNumero = sigNumero,
            FinNumero = finNumero,
            Bloqueado = bloqueado,
            Manual = "N",
            SubTipoDoc = "--",
            TipoSerie = "N",
            IniCadena = "ART-",
            CantDigitos = 4,
            FinCadena = ""
        };

        [Fact]
        public async Task InsertarAsync_SerieAutogenerada_GeneraCodigoYAvanzaSigNumero()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);
            _repoArticuloMock.Setup(r => r.InsertarAsync(It.IsAny<Articulo>())).ReturnsAsync((Articulo a) => a);

            var obj = new Articulo { Serie = 7 };
            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("ART-0005", codigo);
            Assert.Equal("ART-0005", obj.Codigo);
            Assert.Equal(6, serie.SigNumero);
            _repoNumeracionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAutogenerada_IgnoraElCodigoEnviadoPorElCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);
            _repoArticuloMock.Setup(r => r.InsertarAsync(It.IsAny<Articulo>())).ReturnsAsync((Articulo a) => a);

            var obj = new Articulo { Serie = 7, Codigo = "CODIGO-DEL-CLIENTE" };
            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("ART-0005", codigo);
            Assert.Equal("ART-0005", obj.Codigo);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_RespetaCodigoDelCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);
            _repoArticuloMock.Setup(r => r.InsertarAsync(It.IsAny<Articulo>())).ReturnsAsync((Articulo a) => a);

            var obj = new Articulo { Serie = 7, Codigo = "MANUAL-1" };
            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("MANUAL-1", codigo);
            Assert.Equal(5, serie.SigNumero);
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinCodigo_Lanza()
        {
            var serie = SerieAutogenerada();
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);

            var obj = new Articulo { Serie = 7, Codigo = "" };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoArticuloMock.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_PermiteRegistrarIgual()
        {
            // A diferencia de las demás validaciones de la serie, "Bloqueado" ya no impide
            // registrar un artículo -- ver ArticuloDomain.InsertarAsync (cambio confirmado por el
            // usuario, no revierte el chequeo de serie inexistente/manual sin código/sin
            // SigNumero/numeración agotada, que siguen aplicando igual).
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);
            _repoArticuloMock.Setup(r => r.InsertarAsync(It.IsAny<Articulo>())).ReturnsAsync((Articulo a) => a);

            var obj = new Articulo { Serie = 7 };
            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("ART-0005", codigo);
            _repoArticuloMock.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);

            var obj = new Articulo { Serie = 7 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoArticuloMock.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync((NumeracionDocumentoDet?)null);

            var obj = new Articulo { Serie = 7 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoArticuloMock.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_CodigoDuplicado_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);
            _repoArticuloMock.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new List<Articulo> { new() { Codigo = "ART-0005" } }.AsAsyncQueryable());

            var obj = new Articulo { Serie = 7 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoArticuloMock.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }
    }
}
