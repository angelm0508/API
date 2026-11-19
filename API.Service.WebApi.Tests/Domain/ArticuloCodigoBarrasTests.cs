using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>Normalización y validación de <c>CodigoBarras</c> y <c>MostrarEnPos</c> al insertar y actualizar artículos.</summary>
    public class ArticuloCodigoBarrasTests
    {
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArticulo = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly ArticuloDomain _domain;
        private readonly NumeracionDocumentoDet _serie;

        public ArticuloCodigoBarrasTests()
        {
            _domain = new ArticuloDomain(_repoArticulo.Object, _repoNumeracion.Object);
            _serie = new NumeracionDocumentoDet
            {
                CodigoObj = "2", Serie = 7, NombreSerie = "Primario", SigNumero = 5, Bloqueado = "N", Manual = "N",
                SubTipoDoc = "--", TipoSerie = "N", IniCadena = "ART-", CantDigitos = 4, FinCadena = ""
            };
            _repoNumeracion.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(_serie);
            _repoArticulo.Setup(r => r.InsertarAsync(It.IsAny<Articulo>())).ReturnsAsync((Articulo a) => a);
            _repoArticulo.Setup(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<Articulo>())).ReturnsAsync(true);
            Existentes();
        }

        private void Existentes(params Articulo[] articulos) =>
            _repoArticulo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(articulos.AsAsyncQueryable());

        private static Articulo Otro(string codigo, string? barras) => new() { Codigo = codigo, Nombre = "Otro " + codigo, CodigoBarras = barras };

        // ---------- Insertar: código de barras ----------

        [Fact]
        public async Task Insertar_RecortaElCodigoDeBarras()
        {
            var obj = new Articulo { Serie = 7, CodigoBarras = "  7501  " };

            await _domain.InsertarAsync(obj);

            Assert.Equal("7501", obj.CodigoBarras);
            _repoArticulo.Verify(r => r.InsertarAsync(It.Is<Articulo>(a => a.CodigoBarras == "7501")), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Insertar_CodigoDeBarrasVacioOEspacios_SeGuardaComoNull(string? barras)
        {
            var obj = new Articulo { Serie = 7, CodigoBarras = barras };

            await _domain.InsertarAsync(obj);

            Assert.Null(obj.CodigoBarras);
        }

        [Fact]
        public async Task Insertar_CodigoDeBarrasDe50Caracteres_Pasa()
        {
            var obj = new Articulo { Serie = 7, CodigoBarras = new string('9', 50) };

            await _domain.InsertarAsync(obj);

            Assert.Equal(50, obj.CodigoBarras!.Length);
        }

        [Fact]
        public async Task Insertar_CodigoDeBarrasDe51Caracteres_Lanza_YNoNumeraNiInserta()
        {
            var obj = new Articulo { Serie = 7, CodigoBarras = new string('9', 51) };

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));

            Assert.Equal("El código de barras no puede tener más de 50 caracteres.", ex.Message);
            Assert.Equal(5, _serie.SigNumero);
            _repoArticulo.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }

        [Fact]
        public async Task Insertar_LaLongitudSeMideDespuesDeRecortar()
        {
            var obj = new Articulo { Serie = 7, CodigoBarras = "  " + new string('9', 50) + "  " };

            await _domain.InsertarAsync(obj);

            Assert.Equal(new string('9', 50), obj.CodigoBarras);
        }

        [Fact]
        public async Task Insertar_CodigoDeBarrasDeOtroArticulo_Lanza_SinDistinguirMayusculas_YNoNumera()
        {
            Existentes(Otro("ART-0001", "ABC-123"));
            var obj = new Articulo { Serie = 7, CodigoBarras = " abc-123 " };

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));

            Assert.Equal("Ya existe un artículo con el código de barras abc-123.", ex.Message);
            Assert.Equal(5, _serie.SigNumero);
            _repoArticulo.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }

        [Fact]
        public async Task Insertar_CodigoDeBarrasLibre_NoEsDuplicadoAunqueOtrosArticulosTenganBarrasONulo()
        {
            Existentes(Otro("ART-0001", "111"), Otro("ART-0002", null));
            var obj = new Articulo { Serie = 7, CodigoBarras = "222" };

            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("ART-0005", codigo);
            Assert.Equal(6, _serie.SigNumero);
        }

        [Fact]
        public async Task Insertar_DosArticulosSinCodigoDeBarras_NoChocan()
        {
            Existentes(Otro("ART-0001", null));
            var obj = new Articulo { Serie = 7, CodigoBarras = "   " };

            await _domain.InsertarAsync(obj);

            Assert.Null(obj.CodigoBarras);
        }

        [Fact]
        public async Task Insertar_SerieManual_TambienValidaElCodigoDeBarras()
        {
            _serie.Manual = "S";
            Existentes(Otro("M-1", "555"));
            var obj = new Articulo { Serie = 7, Codigo = "M-2", CodigoBarras = "555" };

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));

            Assert.Equal("Ya existe un artículo con el código de barras 555.", ex.Message);
            _repoArticulo.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }

        // ---------- Insertar: MostrarEnPos ----------

        [Theory]
        [InlineData(null, "N")]
        [InlineData("", "N")]
        [InlineData("  ", "N")]
        [InlineData("s", "S")]
        [InlineData("S", "S")]
        [InlineData("n", "N")]
        [InlineData(" s ", "S")]
        public async Task Insertar_MostrarEnPos_NormalizaAMayusculaYVacioEsN(string? entrada, string esperado)
        {
            var obj = new Articulo { Serie = 7, MostrarEnPos = entrada! };

            await _domain.InsertarAsync(obj);

            Assert.Equal(esperado, obj.MostrarEnPos);
        }

        [Theory]
        [InlineData("X")]
        [InlineData("SI")]
        [InlineData("1")]
        public async Task Insertar_MostrarEnPosInvalido_Lanza_YNoNumera(string valor)
        {
            var obj = new Articulo { Serie = 7, MostrarEnPos = valor };

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));

            Assert.Equal("El valor de «Mostrar en el POS» no es válido.", ex.Message);
            Assert.Equal(5, _serie.SigNumero);
            _repoArticulo.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }

        // ---------- Actualizar ----------

        [Fact]
        public async Task Actualizar_RecortaElCodigoDeBarrasYLoEnviaAlRepositorio()
        {
            var obj = new Articulo { Codigo = "A1", CodigoBarras = "  7501  ", MostrarEnPos = "s" };

            await _domain.ActualizarAsync("A1", obj);

            Assert.Equal("7501", obj.CodigoBarras);
            Assert.Equal("S", obj.MostrarEnPos);
            _repoArticulo.Verify(r => r.ActualizarAsync("A1", It.Is<Articulo>(a => a.CodigoBarras == "7501" && a.MostrarEnPos == "S")), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public async Task Actualizar_CodigoDeBarrasVacio_SeGuardaComoNull_YMostrarEnPosVacioComoN(string? barras)
        {
            var obj = new Articulo { Codigo = "A1", CodigoBarras = barras, MostrarEnPos = "" };

            await _domain.ActualizarAsync("A1", obj);

            Assert.Null(obj.CodigoBarras);
            Assert.Equal("N", obj.MostrarEnPos);
        }

        [Fact]
        public async Task Actualizar_CodigoDeBarrasDe51Caracteres_Lanza_SinActualizar()
        {
            var obj = new Articulo { Codigo = "A1", CodigoBarras = new string('9', 51) };

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("A1", obj));

            Assert.Equal("El código de barras no puede tener más de 50 caracteres.", ex.Message);
            _repoArticulo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<Articulo>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_CodigoDeBarrasDeOtroArticulo_Lanza_SinActualizar()
        {
            Existentes(Otro("A1", "111"), Otro("A2", "ABC-9"));
            var obj = new Articulo { Codigo = "A1", CodigoBarras = "abc-9" };

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("A1", obj));

            Assert.Equal("Ya existe un artículo con el código de barras abc-9.", ex.Message);
            _repoArticulo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<Articulo>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_ElPropioArticuloConSuMismoCodigoDeBarras_NoEsDuplicado()
        {
            Existentes(Otro("A1", "111"), Otro("A2", "222"));
            var obj = new Articulo { Codigo = "A1", CodigoBarras = "111" };

            var r = await _domain.ActualizarAsync("A1", obj);

            Assert.True(r);
            _repoArticulo.Verify(x => x.ActualizarAsync("A1", obj), Times.Once);
        }

        [Fact]
        public async Task Actualizar_MostrarEnPosInvalido_Lanza_SinActualizar()
        {
            var obj = new Articulo { Codigo = "A1", MostrarEnPos = "X" };

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("A1", obj));

            Assert.Equal("El valor de «Mostrar en el POS» no es válido.", ex.Message);
            _repoArticulo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<Articulo>()), Times.Never);
        }
    }
}
