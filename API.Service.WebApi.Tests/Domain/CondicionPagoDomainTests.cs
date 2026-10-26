using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class CondicionPagoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<CondicionPago, string>> _repo = new();
        private readonly CondicionPagoDomain _domain;

        public CondicionPagoDomainTests()
        {
            _domain = new CondicionPagoDomain(_repo.Object);
            _repo.Setup(r => r.InsertarAsync(It.IsAny<CondicionPago>())).ReturnsAsync((CondicionPago c) => c);
            _repo.Setup(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<CondicionPago>())).ReturnsAsync(true);
        }

        private void Existe(string codigo, string nombre, int dias, string activo = "S") =>
            _repo.Setup(r => r.ObtenerAsync(codigo)).ReturnsAsync(new CondicionPago { Codigo = codigo, Nombre = nombre, Dias = dias, Activo = activo });

        private async Task<string> MensajeAlInsertar(CondicionPago c)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(c));
            return ex.Message;
        }

        // ---------- InsertarAsync ----------

        [Fact]
        public async Task InsertarAsync_Valida_GuardaConActivoPorDefecto()
        {
            var ok = await _domain.InsertarAsync(new CondicionPago { Codigo = " C20 ", Nombre = " Crédito 20 días ", Dias = 20 });

            Assert.True(ok);
            _repo.Verify(r => r.InsertarAsync(It.Is<CondicionPago>(c => c.Codigo == "C20" && c.Nombre == "Crédito 20 días" && c.Dias == 20 && c.Activo == "S")), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(365)]
        public async Task InsertarAsync_DiasEnLosLimites_Guarda(int dias)
        {
            Assert.True(await _domain.InsertarAsync(new CondicionPago { Codigo = "LIM", Nombre = "Límite", Dias = dias }));

            _repo.Verify(r => r.InsertarAsync(It.Is<CondicionPago>(c => c.Dias == dias)), Times.Once);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(366)]
        public async Task InsertarAsync_DiasFueraDeRango_Lanza(int dias)
        {
            Assert.Equal("Los días de crédito deben estar entre 0 y 365.", await MensajeAlInsertar(new CondicionPago { Codigo = "LIM", Nombre = "Límite", Dias = dias }));
            _repo.Verify(r => r.InsertarAsync(It.IsAny<CondicionPago>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SinCodigo_Lanza()
        {
            Assert.Equal("El código de la condición de pago es requerido.", await MensajeAlInsertar(new CondicionPago { Codigo = "  ", Nombre = "Contado" }));
            _repo.Verify(r => r.InsertarAsync(It.IsAny<CondicionPago>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SinNombre_Lanza()
        {
            Assert.Equal("El nombre de la condición de pago es requerido.", await MensajeAlInsertar(new CondicionPago { Codigo = "CON", Nombre = "" }));
        }

        [Fact]
        public async Task InsertarAsync_ActivoInvalido_Lanza()
        {
            Assert.Equal("El campo Activo debe ser 'S' o 'N'.", await MensajeAlInsertar(new CondicionPago { Codigo = "C20", Nombre = "Crédito 20", Dias = 20, Activo = "X" }));
        }

        [Fact]
        public async Task InsertarAsync_CodigoDuplicado_Lanza()
        {
            Existe("C30", "Crédito 30 días", 30);

            Assert.Equal("Ya existe un registro con el código: C30", await MensajeAlInsertar(new CondicionPago { Codigo = "C30", Nombre = "Otra", Dias = 30 }));
            _repo.Verify(r => r.InsertarAsync(It.IsAny<CondicionPago>()), Times.Never);
        }

        // ---------- ActualizarAsync ----------

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalse()
        {
            Assert.False(await _domain.ActualizarAsync("NOPE", new CondicionPago { Nombre = "X", Dias = 1 }));
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<CondicionPago>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_SinNombre_Lanza()
        {
            Existe("C30", "Crédito 30 días", 30);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("C30", new CondicionPago { Nombre = " ", Dias = 30 }));

            Assert.Equal("El nombre de la condición de pago es requerido.", ex.Message);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(366)]
        public async Task ActualizarAsync_DiasFueraDeRango_Lanza(int dias)
        {
            Existe("C30", "Crédito 30 días", 30);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("C30", new CondicionPago { Nombre = "Crédito 30 días", Dias = dias }));

            Assert.Equal("Los días de crédito deben estar entre 0 y 365.", ex.Message);
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<CondicionPago>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_ActivoInvalido_Lanza()
        {
            Existe("C30", "Crédito 30 días", 30);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("C30", new CondicionPago { Nombre = "Crédito 30 días", Dias = 30, Activo = "X" }));

            Assert.Equal("El campo Activo debe ser 'S' o 'N'.", ex.Message);
        }

        [Fact]
        public async Task ActualizarAsync_CambiaDiasYNombre_Actualiza()
        {
            Existe("C30", "Crédito 30 días", 30);

            Assert.True(await _domain.ActualizarAsync("C30", new CondicionPago { Nombre = " Crédito 35 ", Dias = 35, Activo = "S" }));

            _repo.Verify(r => r.ActualizarAsync("C30", It.Is<CondicionPago>(c => c.Nombre == "Crédito 35" && c.Dias == 35 && c.Activo == "S")), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_ActivoNulo_QuedaActivo()
        {
            Existe("C30", "Crédito 30 días", 30);

            await _domain.ActualizarAsync("C30", new CondicionPago { Nombre = "Crédito 30 días", Dias = 30, Activo = null! });

            _repo.Verify(r => r.ActualizarAsync("C30", It.Is<CondicionPago>(c => c.Activo == "S")), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_DesactivarCON_Lanza()
        {
            Existe("CON", "Contado", 0);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("CON", new CondicionPago { Nombre = "Contado", Dias = 0, Activo = "N" }));

            Assert.Equal("La condición CON es la predeterminada del sistema y no se puede dar de baja.", ex.Message);
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<CondicionPago>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CONSigueActiva_Actualiza()
        {
            Existe("CON", "Contado", 0);

            Assert.True(await _domain.ActualizarAsync("CON", new CondicionPago { Nombre = "Contado inmediato", Dias = 0, Activo = "S" }));
        }

        [Theory]
        [InlineData("CON")]
        [InlineData("con")]
        public async Task ActualizarAsync_CONConDiasDistintosDeCero_Lanza(string codigo)
        {
            Existe(codigo, "Contado", 0);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(codigo, new CondicionPago { Nombre = "Contado", Dias = 30, Activo = "S" }));

            Assert.Equal("La condición CON debe tener 0 días.", ex.Message);
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<CondicionPago>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CONConCeroDiasYNuevoNombre_Actualiza()
        {
            Existe("CON", "Contado", 0);

            Assert.True(await _domain.ActualizarAsync("CON", new CondicionPago { Nombre = "Pago de contado", Dias = 0, Activo = "S" }));

            _repo.Verify(r => r.ActualizarAsync("CON", It.Is<CondicionPago>(c => c.Nombre == "Pago de contado" && c.Dias == 0)), Times.Once);
        }

        // ---------- EliminarAsync ----------

        [Fact]
        public async Task EliminarAsync_Inexistente_DevuelveFalse()
        {
            Assert.False(await _domain.EliminarAsync("NOPE"));
        }

        [Fact]
        public async Task EliminarAsync_BajaLogica_ConservaNombreYDias()
        {
            Existe("C30", "Crédito 30 días", 30);

            Assert.True(await _domain.EliminarAsync("C30"));

            _repo.Verify(r => r.ActualizarAsync("C30", It.Is<CondicionPago>(c => c.Activo == "N" && c.Nombre == "Crédito 30 días" && c.Dias == 30)), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_YaInactiva_NoVuelveAActualizar()
        {
            Existe("C30", "Crédito 30 días", 30, "N");

            Assert.True(await _domain.EliminarAsync("C30"));

            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<CondicionPago>()), Times.Never);
        }

        [Theory]
        [InlineData("CON")]
        [InlineData("con")]
        public async Task EliminarAsync_CON_Lanza(string codigo)
        {
            Existe(codigo, "Contado", 0);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(codigo));

            Assert.Equal("La condición CON es la predeterminada del sistema y no se puede dar de baja.", ex.Message);
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<CondicionPago>()), Times.Never);
        }

        // ---------- ObtenerActivaAsync ----------

        [Fact]
        public async Task ObtenerActivaAsync_Activa_LaDevuelve()
        {
            Existe("C30", "Crédito 30 días", 30);

            var condicion = await _domain.ObtenerActivaAsync("C30");

            Assert.NotNull(condicion);
            Assert.Equal(30, condicion!.Dias);
        }

        [Fact]
        public async Task ObtenerActivaAsync_Inexistente_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ObtenerActivaAsync("XXX"));

            Assert.Equal("La condición de pago XXX no existe.", ex.Message);
        }

        [Fact]
        public async Task ObtenerActivaAsync_Inactiva_Lanza()
        {
            Existe("C45", "Crédito 45 días", 45, "N");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ObtenerActivaAsync("C45"));

            Assert.Equal("La condición de pago C45 está inactiva.", ex.Message);
        }

        // ---------- Consultas ----------

        [Fact]
        public async Task ObtenerPorCodigoAsync_DelegaEnElRepositorio()
        {
            Existe("C15", "Crédito 15 días", 15);

            var condicion = await _domain.ObtenerPorCodigoAsync("C15");

            Assert.Equal("C15", condicion!.Codigo);
            Assert.Null(await _domain.ObtenerPorCodigoAsync("NOPE"));
        }
    }
}
