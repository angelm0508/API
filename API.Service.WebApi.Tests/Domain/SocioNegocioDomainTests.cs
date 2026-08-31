using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class SocioNegocioDomainTests
    {
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocioMock;
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracionMock;
        private readonly SocioNegocioDomain _domain;

        public SocioNegocioDomainTests()
        {
            _repoSocioMock = new Mock<IRepositorioGenerico<SocioNegocio, string>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new SocioNegocioDomain(_repoSocioMock.Object, _repoNumeracionMock.Object);

            _repoSocioMock.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new List<SocioNegocio>().AsAsyncQueryable());
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "1",
            Serie = 9,
            NombreSerie = "Primario",
            SigNumero = sigNumero,
            FinNumero = finNumero,
            Bloqueado = bloqueado,
            Manual = "N",
            SubTipoDoc = "C",
            TipoSerie = "N",
            IniCadena = "CLI-",
            CantDigitos = 4,
            FinCadena = ""
        };

        private void SeedSocios() => _repoSocioMock
            .Setup(r => r.ObtenerTodoAsync())
            .ReturnsAsync(new[]
            {
                new SocioNegocio { Codigo = "C001", Nombre = "Cliente Uno", TipoSn = "C" },
                new SocioNegocio { Codigo = "C002", Nombre = "Cliente Dos Nube", TipoSn = "C" },
                new SocioNegocio { Codigo = "P001", Nombre = "Proveedor Uno", TipoSn = "P" },
                new SocioNegocio { Codigo = "P002", Nombre = "Proveedor Dos Nube", TipoSn = "P" },
            }.AsAsyncQueryable());

        [Fact]
        public async Task ObtenerContengaNombreAsync_SinTipo_NoFiltra()
        {
            SeedSocios();
            var r = await _domain.ObtenerContengaNombreAsync("Nube");
            Assert.Equal(2, r.Count());
        }

        [Fact]
        public async Task ObtenerContengaNombreAsync_TipoP_SoloProveedores()
        {
            SeedSocios();
            var r = await _domain.ObtenerContengaNombreAsync("Nube", "P");
            Assert.Single(r);
            Assert.Equal("P002", r.First().Codigo);
        }

        [Fact]
        public async Task ObtenerContengaNombreAsync_TipoC_SoloClientes()
        {
            SeedSocios();
            var r = await _domain.ObtenerContengaNombreAsync("Uno", "C");
            Assert.Single(r);
            Assert.Equal("C001", r.First().Codigo);
        }

        [Fact]
        public async Task ObtenerTodoAsync_TipoP_SoloProveedores()
        {
            SeedSocios();
            var q = await _domain.ObtenerTodoAsync("P");
            Assert.Equal(2, q.Count());
            Assert.All(q, s => Assert.Equal("P", s.TipoSn));
        }

        [Fact]
        public async Task ObtenerTodoAsync_SinTipo_DevuelveTodos()
        {
            SeedSocios();
            var q = await _domain.ObtenerTodoAsync();
            Assert.Equal(4, q.Count());
        }

        [Fact]
        public async Task ObtenerTodoAsync_TipoInvalido_NoFiltra()
        {
            SeedSocios();
            var q = await _domain.ObtenerTodoAsync("X");
            Assert.Equal(4, q.Count());
        }

        [Fact]
        public async Task InsertarAsync_SerieAutogenerada_GeneraCodigoYAvanzaSigNumero()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);
            _repoSocioMock.Setup(r => r.InsertarAsync(It.IsAny<SocioNegocio>())).ReturnsAsync((SocioNegocio s) => s);

            var obj = new SocioNegocio { Serie = 9 };
            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("CLI-0005", codigo);
            Assert.Equal("CLI-0005", obj.Codigo);
            Assert.Equal(6, serie.SigNumero);
            _repoNumeracionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAutogenerada_IgnoraElCodigoEnviadoPorElCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);
            _repoSocioMock.Setup(r => r.InsertarAsync(It.IsAny<SocioNegocio>())).ReturnsAsync((SocioNegocio s) => s);

            var obj = new SocioNegocio { Serie = 9, Codigo = "CODIGO-DEL-CLIENTE" };
            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("CLI-0005", codigo);
            Assert.Equal("CLI-0005", obj.Codigo);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_RespetaCodigoDelCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);
            _repoSocioMock.Setup(r => r.InsertarAsync(It.IsAny<SocioNegocio>())).ReturnsAsync((SocioNegocio s) => s);

            var obj = new SocioNegocio { Serie = 9, Codigo = "MANUAL-1" };
            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("MANUAL-1", codigo);
            Assert.Equal(5, serie.SigNumero);
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinCodigo_Lanza()
        {
            var serie = SerieAutogenerada();
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);

            var obj = new SocioNegocio { Serie = 9, Codigo = "" };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_PermiteRegistrarIgual()
        {
            // A diferencia de las demás validaciones de la serie, "Bloqueado" ya no impide
            // registrar un socio de negocio -- ver SocioNegocioDomain.InsertarAsync (cambio
            // confirmado por el usuario; los chequeos de serie inexistente / manual sin código /
            // sin SigNumero / numeración agotada siguen aplicando igual).
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);
            _repoSocioMock.Setup(r => r.InsertarAsync(It.IsAny<SocioNegocio>())).ReturnsAsync((SocioNegocio s) => s);

            var obj = new SocioNegocio { Serie = 9 };
            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("CLI-0005", codigo);
            Assert.Equal(6, serie.SigNumero);
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);

            var obj = new SocioNegocio { Serie = 9 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync((NumeracionDocumentoDet?)null);

            var obj = new SocioNegocio { Serie = 9 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_CodigoDuplicado_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);
            _repoSocioMock.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new List<SocioNegocio> { new() { Codigo = "CLI-0005" } }.AsAsyncQueryable());

            var obj = new SocioNegocio { Serie = 9 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }
    }
}
