using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class SalidaMercanciaDetalleDomainTests
    {
        private readonly Mock<IRepositorioGenerico<SalidaMercanciaDetalle, (int Entry, int NoLinea)>> _repoDet = new();
        private readonly Mock<IRepositorioGenerico<SalidaMercancia, int>> _repoHeader = new();
        private readonly SalidaMercanciaDetalleDomain _domain;

        public SalidaMercanciaDetalleDomainTests()
        {
            _domain = new SalidaMercanciaDetalleDomain(_repoDet.Object, _repoHeader.Object);
        }

        [Fact]
        public async Task InsertarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new SalidaMercancia { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new SalidaMercanciaDetalle { Entry = 7 }));
            _repoDet.Verify(r => r.InsertarAsync(It.IsAny<SalidaMercanciaDetalle>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_DocumentoNoExiste_Lanza()
        {
            // Sin encabezado sembrado: el insert suelto de líneas se rechaza siempre
            // (sin FK a SalidaMercancia, un Entry inexistente generaría una línea huérfana).
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new SalidaMercanciaDetalle { Entry = 7 }));
            _repoDet.Verify(r => r.InsertarAsync(It.IsAny<SalidaMercanciaDetalle>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new SalidaMercancia { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(7, 1, new SalidaMercanciaDetalle()));
        }

        [Fact]
        public async Task EliminarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new SalidaMercancia { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(7, 1));
        }
    }
}
