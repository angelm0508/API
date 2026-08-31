using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class EntregaCompraDetalleDomainTests
    {
        private readonly Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>> _repoDet = new();
        private readonly Mock<IRepositorioGenerico<EntregaCompra, int>> _repoHeader = new();
        private readonly EntregaCompraDetalleDomain _domain;

        public EntregaCompraDetalleDomainTests()
        {
            _domain = new EntregaCompraDetalleDomain(_repoDet.Object, _repoHeader.Object);
        }

        [Fact]
        public async Task InsertarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new EntregaCompra { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompraDetalle { Entry = 7 }));
            _repoDet.Verify(r => r.InsertarAsync(It.IsAny<EntregaCompraDetalle>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new EntregaCompra { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(7, 1, new EntregaCompraDetalle()));
        }

        [Fact]
        public async Task EliminarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new EntregaCompra { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(7, 1));
        }
    }
}
