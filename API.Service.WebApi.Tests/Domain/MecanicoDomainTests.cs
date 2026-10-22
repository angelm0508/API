using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class MecanicoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Mecanico, string>> _repo = new();
        private readonly MecanicoDomain _domain;

        public MecanicoDomainTests()
        {
            _domain = new MecanicoDomain(_repo.Object);
            _repo.Setup(r => r.InsertarAsync(It.IsAny<Mecanico>())).ReturnsAsync((Mecanico m) => m);
            _repo.Setup(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<Mecanico>())).ReturnsAsync(true);
        }

        private async Task<string> MensajeAlInsertar(Mecanico m)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(m));
            return ex.Message;
        }

        [Fact]
        public async Task InsertarAsync_Valido_GuardaConActivoPorDefecto()
        {
            var ok = await _domain.InsertarAsync(new Mecanico { Codigo = " M01 ", Nombre = " Juan Pérez " });

            Assert.True(ok);
            _repo.Verify(r => r.InsertarAsync(It.Is<Mecanico>(m => m.Codigo == "M01" && m.Nombre == "Juan Pérez" && m.Activo == "S")), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SinCodigo_Lanza()
        {
            Assert.Equal("El código del mecánico es requerido.", await MensajeAlInsertar(new Mecanico { Codigo = "  ", Nombre = "Juan" }));
            _repo.Verify(r => r.InsertarAsync(It.IsAny<Mecanico>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SinNombre_Lanza()
        {
            Assert.Equal("El nombre del mecánico es requerido.", await MensajeAlInsertar(new Mecanico { Codigo = "M01", Nombre = "" }));
        }

        [Fact]
        public async Task InsertarAsync_CodigoDuplicado_Lanza()
        {
            _repo.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Otro", Activo = "S" });

            Assert.Equal("Ya existe un registro con el código: M01", await MensajeAlInsertar(new Mecanico { Codigo = "M01", Nombre = "Juan" }));
        }

        [Fact]
        public async Task InsertarAsync_ActivoInvalido_Lanza()
        {
            Assert.Equal("El campo Activo debe ser 'S' o 'N'.", await MensajeAlInsertar(new Mecanico { Codigo = "M01", Nombre = "Juan", Activo = "X" }));
        }

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalso()
        {
            _repo.Setup(r => r.ObtenerAsync("M99")).ReturnsAsync((Mecanico?)null);

            Assert.False(await _domain.ActualizarAsync("M99", new Mecanico { Nombre = "X", Activo = "S" }));
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<Mecanico>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_SinNombre_Lanza()
        {
            _repo.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Juan", Activo = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("M01", new Mecanico { Nombre = " ", Activo = "S" }));
            Assert.Equal("El nombre del mecánico es requerido.", ex.Message);
        }

        [Fact]
        public async Task ActualizarAsync_Valido_Delega()
        {
            _repo.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Juan", Activo = "S" });

            Assert.True(await _domain.ActualizarAsync("M01", new Mecanico { Nombre = "Juan P.", Telefono = "5555", Activo = "N" }));
            _repo.Verify(r => r.ActualizarAsync("M01", It.Is<Mecanico>(m => m.Nombre == "Juan P." && m.Telefono == "5555" && m.Activo == "N")), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_HaceBajaLogica()
        {
            _repo.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Juan", Telefono = "5555", Activo = "S" });

            Assert.True(await _domain.EliminarAsync("M01"));
            _repo.Verify(r => r.ActualizarAsync("M01", It.Is<Mecanico>(m => m.Nombre == "Juan" && m.Telefono == "5555" && m.Activo == "N")), Times.Once);
            _repo.Verify(r => r.EliminarAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_YaInactivo_DevuelveVerdaderoSinGuardar()
        {
            _repo.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Juan", Activo = "N" });

            Assert.True(await _domain.EliminarAsync("M01"));
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<Mecanico>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_Inexistente_DevuelveFalso()
        {
            _repo.Setup(r => r.ObtenerAsync("M99")).ReturnsAsync((Mecanico?)null);

            Assert.False(await _domain.EliminarAsync("M99"));
        }
    }
}
