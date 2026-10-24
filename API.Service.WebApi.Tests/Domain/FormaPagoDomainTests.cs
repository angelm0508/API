using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class FormaPagoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<FormaPago, string>> _repo = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)>> _repoMedio = new();
        private readonly FormaPagoDomain _domain;

        public FormaPagoDomainTests()
        {
            _domain = new FormaPagoDomain(_repo.Object, _repoMedio.Object);
            _repo.Setup(r => r.InsertarAsync(It.IsAny<FormaPago>())).ReturnsAsync((FormaPago f) => f);
            _repo.Setup(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<FormaPago>())).ReturnsAsync(true);
            _repoMedio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PagoRecibidoMedio>().AsAsyncQueryable());
        }

        private void MedioUsaForma(string codigo) =>
            _repoMedio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PagoRecibidoMedio>
            {
                new() { Entry = 1, NoLinea = 1, CodFormaPago = codigo, Monto = 10m }
            }.AsAsyncQueryable());

        private async Task<string> MensajeAlInsertar(FormaPago f)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(f));
            return ex.Message;
        }

        [Fact]
        public async Task InsertarAsync_Valida_GuardaConTipoYActivoPorDefecto()
        {
            var ok = await _domain.InsertarAsync(new FormaPago { Codigo = " EFE ", Nombre = " Efectivo " });

            Assert.True(ok);
            _repo.Verify(r => r.InsertarAsync(It.Is<FormaPago>(f => f.Codigo == "EFE" && f.Nombre == "Efectivo" && f.TipoDato == "N" && f.Activo == "S")), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SinCodigo_Lanza()
        {
            Assert.Equal("El código de la forma de pago es requerido.", await MensajeAlInsertar(new FormaPago { Codigo = "  ", Nombre = "Efectivo" }));
            _repo.Verify(r => r.InsertarAsync(It.IsAny<FormaPago>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SinNombre_Lanza()
        {
            Assert.Equal("El nombre de la forma de pago es requerido.", await MensajeAlInsertar(new FormaPago { Codigo = "EFE", Nombre = "" }));
        }

        [Fact]
        public async Task InsertarAsync_TipoDatoInvalido_Lanza()
        {
            Assert.Equal("El tipo de dato debe ser 'N', 'C', 'T' o 'J'.", await MensajeAlInsertar(new FormaPago { Codigo = "EFE", Nombre = "Efectivo", TipoDato = "X" }));
        }

        [Fact]
        public async Task InsertarAsync_ActivoInvalido_Lanza()
        {
            Assert.Equal("El campo Activo debe ser 'S' o 'N'.", await MensajeAlInsertar(new FormaPago { Codigo = "EFE", Nombre = "Efectivo", Activo = "X" }));
        }

        [Fact]
        public async Task InsertarAsync_CodigoDuplicado_Lanza()
        {
            _repo.Setup(r => r.ObtenerAsync("EFE")).ReturnsAsync(new FormaPago { Codigo = "EFE", Nombre = "Otro", TipoDato = "N", Activo = "S" });

            Assert.Equal("Ya existe un registro con el código: EFE", await MensajeAlInsertar(new FormaPago { Codigo = "EFE", Nombre = "Efectivo" }));
        }

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalse()
        {
            Assert.False(await _domain.ActualizarAsync("NOPE", new FormaPago { Nombre = "X" }));
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<FormaPago>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_SinNombre_Lanza()
        {
            _repo.Setup(r => r.ObtenerAsync("EFE")).ReturnsAsync(new FormaPago { Codigo = "EFE", Nombre = "Efectivo", TipoDato = "N", Activo = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("EFE", new FormaPago { Nombre = " " }));

            Assert.Equal("El nombre de la forma de pago es requerido.", ex.Message);
        }

        [Fact]
        public async Task ActualizarAsync_TipoDatoNulo_ConservaElActual()
        {
            _repo.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "S" });

            await _domain.ActualizarAsync("CHQ", new FormaPago { Nombre = "Cheque bancario", TipoDato = null!, Activo = "S" });

            _repo.Verify(r => r.ActualizarAsync("CHQ", It.Is<FormaPago>(f => f.Nombre == "Cheque bancario" && f.TipoDato == "C")), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_CambiaTipoDatoConPagosQueLaUsan_Lanza()
        {
            _repo.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "S" });
            MedioUsaForma("CHQ");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("CHQ", new FormaPago { Nombre = "Cheque", TipoDato = "T", Activo = "S" }));

            Assert.Equal("No se puede cambiar el tipo de dato: la forma de pago ya se usó en pagos.", ex.Message);
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<FormaPago>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CambiaTipoDatoSinPagos_Actualiza()
        {
            _repo.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "S" });

            Assert.True(await _domain.ActualizarAsync("CHQ", new FormaPago { Nombre = "Cheque", TipoDato = "T", Activo = "S" }));
        }

        [Fact]
        public async Task ActualizarAsync_MismoTipoDatoConPagos_Actualiza()
        {
            _repo.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "S" });
            MedioUsaForma("CHQ");

            Assert.True(await _domain.ActualizarAsync("CHQ", new FormaPago { Nombre = "Cheque bancario", TipoDato = "C", Activo = "S" }));
        }

        [Fact]
        public async Task EliminarAsync_Inexistente_DevuelveFalse()
        {
            Assert.False(await _domain.EliminarAsync("NOPE"));
        }

        [Fact]
        public async Task EliminarAsync_BajaLogica_ConservaNombreYTipo()
        {
            _repo.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "S" });

            Assert.True(await _domain.EliminarAsync("CHQ"));

            _repo.Verify(r => r.ActualizarAsync("CHQ", It.Is<FormaPago>(f => f.Activo == "N" && f.Nombre == "Cheque" && f.TipoDato == "C")), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_YaInactiva_NoVuelveAActualizar()
        {
            _repo.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "N" });

            Assert.True(await _domain.EliminarAsync("CHQ"));

            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<FormaPago>()), Times.Never);
        }
    }
}
