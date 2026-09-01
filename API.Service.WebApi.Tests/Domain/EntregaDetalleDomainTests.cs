using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class EntregaDetalleDomainTests
    {
        private readonly Mock<IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)>> _repoDet = new();
        private readonly Mock<IRepositorioGenerico<Entrega, int>> _repoHeader = new();
        private readonly EntregaDetalleDomain _domain;

        public EntregaDetalleDomainTests()
        {
            _domain = new EntregaDetalleDomain(_repoDet.Object, _repoHeader.Object);
        }

        [Fact]
        public async Task InsertarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Entrega { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaDetalle { Entry = 7 }));
            _repoDet.Verify(r => r.InsertarAsync(It.IsAny<EntregaDetalle>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_DocumentoNoExiste_Lanza()
        {
            // Sin encabezado sembrado: el insert suelto de líneas se rechaza siempre
            // (sin FK a Entrega, un Entry inexistente generaría una línea huérfana).
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaDetalle { Entry = 7 }));
            _repoDet.Verify(r => r.InsertarAsync(It.IsAny<EntregaDetalle>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Entrega { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(7, 1, new EntregaDetalle()));
        }

        [Fact]
        public async Task EliminarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Entrega { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(7, 1));
        }
    }
}
