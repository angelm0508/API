using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
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
        private readonly Mock<ICondicionPagoDomain> _condicionesMock;
        private readonly SocioNegocioDomain _domain;

        public SocioNegocioDomainTests()
        {
            _repoSocioMock = new Mock<IRepositorioGenerico<SocioNegocio, string>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _condicionesMock = new Mock<ICondicionPagoDomain>();
            _domain = new SocioNegocioDomain(_repoSocioMock.Object, _repoNumeracionMock.Object, _condicionesMock.Object);

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

        // ---------------- condición de pago por defecto del socio ----------------

        private void CondicionActiva(string codigo, int dias = 30) => _condicionesMock
            .Setup(c => c.ObtenerActivaAsync(codigo))
            .ReturnsAsync(new CondicionPago { Codigo = codigo, Nombre = codigo, Dias = dias, Activo = "S" });

        [Fact]
        public async Task InsertarAsync_ConCondicionValida_LaGuarda()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(SerieAutogenerada(sigNumero: 5));
            _repoSocioMock.Setup(r => r.InsertarAsync(It.IsAny<SocioNegocio>())).ReturnsAsync((SocioNegocio s) => s);
            CondicionActiva("C30");

            var obj = new SocioNegocio { Serie = 9, CondicionPago = "C30" };
            await _domain.InsertarAsync(obj);

            Assert.Equal("C30", obj.CondicionPago);
            _condicionesMock.Verify(c => c.ObtenerActivaAsync("C30"), Times.Once);
            _repoSocioMock.Verify(r => r.InsertarAsync(obj), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_ConCondicionInexistente_Lanza_NoAvanzaElConsecutivoNiGuarda()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);
            _condicionesMock.Setup(c => c.ObtenerActivaAsync("ZZZ")).ThrowsAsync(new Exception("La condición de pago ZZZ no existe."));

            var obj = new SocioNegocio { Serie = 9, CondicionPago = "ZZZ" };
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));

            Assert.Equal("La condición de pago ZZZ no existe.", ex.Message);
            Assert.Equal(5, serie.SigNumero);
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_ConCondicionInactiva_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(SerieAutogenerada(sigNumero: 5));
            _condicionesMock.Setup(c => c.ObtenerActivaAsync("C60")).ThrowsAsync(new Exception("La condición de pago C60 está inactiva."));

            var obj = new SocioNegocio { Serie = 9, CondicionPago = "C60" };
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));

            Assert.Equal("La condición de pago C60 está inactiva.", ex.Message);
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task InsertarAsync_SinCondicion_SeGuardaNula_YNoConsultaCondiciones(string? condicion)
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(SerieAutogenerada(sigNumero: 5));
            _repoSocioMock.Setup(r => r.InsertarAsync(It.IsAny<SocioNegocio>())).ReturnsAsync((SocioNegocio s) => s);

            var obj = new SocioNegocio { Serie = 9, CondicionPago = condicion };
            await _domain.InsertarAsync(obj);

            Assert.Null(obj.CondicionPago);
            _condicionesMock.Verify(c => c.ObtenerActivaAsync(It.IsAny<string>()), Times.Never);
            _repoSocioMock.Verify(r => r.InsertarAsync(obj), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_CondicionConEspacios_SeRecortaAntesDeValidar()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(SerieAutogenerada(sigNumero: 5));
            _repoSocioMock.Setup(r => r.InsertarAsync(It.IsAny<SocioNegocio>())).ReturnsAsync((SocioNegocio s) => s);
            CondicionActiva("C15", 15);

            var obj = new SocioNegocio { Serie = 9, CondicionPago = " C15 " };
            await _domain.InsertarAsync(obj);

            Assert.Equal("C15", obj.CondicionPago);
            _condicionesMock.Verify(c => c.ObtenerActivaAsync("C15"), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_ConCondicionValida_LaGuarda()
        {
            CondicionActiva("C45", 45);
            _repoSocioMock.Setup(r => r.ActualizarAsync("C001", It.IsAny<SocioNegocio>())).ReturnsAsync(true);

            var obj = new SocioNegocio { Nombre = "Cliente Uno", CondicionPago = "C45" };
            var resultado = await _domain.ActualizarAsync("C001", obj);

            Assert.True(resultado);
            _repoSocioMock.Verify(r => r.ActualizarAsync("C001", It.Is<SocioNegocio>(s => s.CondicionPago == "C45")), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_ConCondicionInexistente_Lanza_NoActualiza()
        {
            _condicionesMock.Setup(c => c.ObtenerActivaAsync("ZZZ")).ThrowsAsync(new Exception("La condición de pago ZZZ no existe."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("C001", new SocioNegocio { CondicionPago = "ZZZ" }));

            Assert.Equal("La condición de pago ZZZ no existe.", ex.Message);
            _repoSocioMock.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_ConCondicionInactiva_Lanza_NoActualiza()
        {
            _condicionesMock.Setup(c => c.ObtenerActivaAsync("C60")).ThrowsAsync(new Exception("La condición de pago C60 está inactiva."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("C001", new SocioNegocio { CondicionPago = "C60" }));

            Assert.Equal("La condición de pago C60 está inactiva.", ex.Message);
            _repoSocioMock.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ActualizarAsync_SinCondicion_SeGuardaNula_YNoConsultaCondiciones(string? condicion)
        {
            _repoSocioMock.Setup(r => r.ActualizarAsync("C001", It.IsAny<SocioNegocio>())).ReturnsAsync(true);

            var obj = new SocioNegocio { Nombre = "Cliente Uno", CondicionPago = condicion };
            await _domain.ActualizarAsync("C001", obj);

            Assert.Null(obj.CondicionPago);
            _condicionesMock.Verify(c => c.ObtenerActivaAsync(It.IsAny<string>()), Times.Never);
            _repoSocioMock.Verify(r => r.ActualizarAsync("C001", It.Is<SocioNegocio>(s => s.CondicionPago == null)), Times.Once);
        }

        // ---------------- límite de crédito y bloqueo por vencido ----------------

        private void PrepararInsercionConSerie()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(SerieAutogenerada(sigNumero: 5));
            _repoSocioMock.Setup(r => r.InsertarAsync(It.IsAny<SocioNegocio>())).ReturnsAsync((SocioNegocio s) => s);
        }

        [Fact]
        public async Task InsertarAsync_ClienteConLimiteYBloqueo_LosGuarda()
        {
            PrepararInsercionConSerie();
            var obj = new SocioNegocio { Serie = 9, TipoSn = "C", LimiteCredito = 5000.50m, BloquearSiVencido = "S" };

            await _domain.InsertarAsync(obj);

            _repoSocioMock.Verify(r => r.InsertarAsync(It.Is<SocioNegocio>(s => s.LimiteCredito == 5000.50m && s.BloquearSiVencido == "S")), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_LimiteNegativo_Lanza_NoInsertaNiConsumeConsecutivo()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new SocioNegocio { Serie = 9, TipoSn = "C", LimiteCredito = -1m }));

            Assert.Equal("El límite de crédito no puede ser negativo.", ex.Message);
            Assert.Equal(5, serie.SigNumero);
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_LimiteConMasDeDosDecimales_Lanza()
        {
            PrepararInsercionConSerie();

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new SocioNegocio { Serie = 9, TipoSn = "C", LimiteCredito = 10.123m }));

            Assert.Equal("El límite de crédito admite como máximo 2 decimales.", ex.Message);
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Theory]
        [InlineData("10")]
        [InlineData("10.1")]
        [InlineData("10.10")]
        [InlineData("10.12")]
        [InlineData("0")]
        public async Task InsertarAsync_LimiteConHastaDosDecimales_EsValido(string limite)
        {
            PrepararInsercionConSerie();

            await _domain.InsertarAsync(new SocioNegocio { Serie = 9, TipoSn = "C", LimiteCredito = decimal.Parse(limite, System.Globalization.CultureInfo.InvariantCulture) });

            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_LimiteCero_EsValido_SoloContado()
        {
            PrepararInsercionConSerie();

            await _domain.InsertarAsync(new SocioNegocio { Serie = 9, TipoSn = "C", LimiteCredito = 0m });

            _repoSocioMock.Verify(r => r.InsertarAsync(It.Is<SocioNegocio>(s => s.LimiteCredito == 0m)), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_ProveedorConLimite_Lanza_NoInserta()
        {
            PrepararInsercionConSerie();

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new SocioNegocio { Serie = 9, TipoSn = "P", LimiteCredito = 100m }));

            Assert.Equal("El límite de crédito solo aplica a clientes.", ex.Message);
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_ProveedorConLimiteCero_Lanza()
        {
            PrepararInsercionConSerie();

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new SocioNegocio { Serie = 9, TipoSn = "P", LimiteCredito = 0m }));

            Assert.Equal("El límite de crédito solo aplica a clientes.", ex.Message);
        }

        [Fact]
        public async Task InsertarAsync_ProveedorSinLimite_Pasa_YElBloqueoSeGuardaComoN()
        {
            PrepararInsercionConSerie();

            await _domain.InsertarAsync(new SocioNegocio { Serie = 9, TipoSn = "P", LimiteCredito = null, BloquearSiVencido = "S" });

            _repoSocioMock.Verify(r => r.InsertarAsync(It.Is<SocioNegocio>(s => s.LimiteCredito == null && s.BloquearSiVencido == "N")), Times.Once);
        }

        [Theory]
        [InlineData("X")]
        [InlineData("s")]
        [InlineData("SI")]
        public async Task InsertarAsync_BloqueoInvalido_Lanza_NoInserta(string bloqueo)
        {
            PrepararInsercionConSerie();

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new SocioNegocio { Serie = 9, TipoSn = "C", BloquearSiVencido = bloqueo }));

            Assert.Equal("El bloqueo por vencido debe ser S o N.", ex.Message);
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task InsertarAsync_BloqueoVacioONulo_SeGuardaComoN(string? bloqueo)
        {
            PrepararInsercionConSerie();

            await _domain.InsertarAsync(new SocioNegocio { Serie = 9, TipoSn = "C", BloquearSiVencido = bloqueo! });

            _repoSocioMock.Verify(r => r.InsertarAsync(It.Is<SocioNegocio>(s => s.BloquearSiVencido == "N")), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_ReemplazaLimiteYBloqueo()
        {
            _repoSocioMock.Setup(r => r.ActualizarAsync("C001", It.IsAny<SocioNegocio>())).ReturnsAsync(true);

            var resultado = await _domain.ActualizarAsync("C001", new SocioNegocio { Codigo = "C001", TipoSn = "C", LimiteCredito = 1200m, BloquearSiVencido = "S" });

            Assert.True(resultado);
            _repoSocioMock.Verify(r => r.ActualizarAsync("C001", It.Is<SocioNegocio>(s => s.LimiteCredito == 1200m && s.BloquearSiVencido == "S")), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_LimiteNulo_LoLimpia()
        {
            _repoSocioMock.Setup(r => r.ActualizarAsync("C001", It.IsAny<SocioNegocio>())).ReturnsAsync(true);

            await _domain.ActualizarAsync("C001", new SocioNegocio { Codigo = "C001", TipoSn = "C", LimiteCredito = null, BloquearSiVencido = "" });

            _repoSocioMock.Verify(r => r.ActualizarAsync("C001", It.Is<SocioNegocio>(s => s.LimiteCredito == null && s.BloquearSiVencido == "N")), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_LimiteNegativo_Lanza_NoActualiza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("C001", new SocioNegocio { TipoSn = "C", LimiteCredito = -0.01m }));

            Assert.Equal("El límite de crédito no puede ser negativo.", ex.Message);
            _repoSocioMock.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_LimiteConTresDecimales_Lanza_NoActualiza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("C001", new SocioNegocio { TipoSn = "C", LimiteCredito = 1.005m }));

            Assert.Equal("El límite de crédito admite como máximo 2 decimales.", ex.Message);
            _repoSocioMock.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_ProveedorConLimite_Lanza_NoActualiza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("P001", new SocioNegocio { TipoSn = "P", LimiteCredito = 10m }));

            Assert.Equal("El límite de crédito solo aplica a clientes.", ex.Message);
            _repoSocioMock.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_BloqueoInvalido_Lanza_NoActualiza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("C001", new SocioNegocio { TipoSn = "C", BloquearSiVencido = "X" }));

            Assert.Equal("El bloqueo por vencido debe ser S o N.", ex.Message);
            _repoSocioMock.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_ProveedorConBloqueoS_SeGuardaComoN()
        {
            _repoSocioMock.Setup(r => r.ActualizarAsync("P001", It.IsAny<SocioNegocio>())).ReturnsAsync(true);

            await _domain.ActualizarAsync("P001", new SocioNegocio { Codigo = "P001", TipoSn = "P", BloquearSiVencido = "S" });

            _repoSocioMock.Verify(r => r.ActualizarAsync("P001", It.Is<SocioNegocio>(s => s.BloquearSiVencido == "N")), Times.Once);
        }
    }
}
