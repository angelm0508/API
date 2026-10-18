using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class FacturaCompraDetalleDomainTests
    {
        private readonly Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>> _repoDet = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoHeader = new();
        private readonly FacturaCompraDetalleDomain _domain;

        public FacturaCompraDetalleDomainTests()
        {
            _domain = new FacturaCompraDetalleDomain(_repoDet.Object, _repoHeader.Object);
        }

        [Fact]
        public async Task InsertarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new FacturaCompra { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new FacturaCompraDetalle { Entry = 7 }));
            _repoDet.Verify(r => r.InsertarAsync(It.IsAny<FacturaCompraDetalle>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_DocumentoNoExiste_Lanza()
        {
            // Sin encabezado sembrado: el insert suelto de líneas se rechaza siempre
            // (sin FK a FacturaCompra, un Entry inexistente generaría una línea huérfana).
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new FacturaCompraDetalle { Entry = 7 }));
            _repoDet.Verify(r => r.InsertarAsync(It.IsAny<FacturaCompraDetalle>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new FacturaCompra { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(7, 1, new FacturaCompraDetalle()));
        }

        [Fact]
        public async Task EliminarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new FacturaCompra { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(7, 1));
        }

        // ---------------- Las líneas de notas de débito no se exponen por este recurso ----------------

        [Fact]
        public async Task ObtenerPorFacturaCompraAsync_DeUnaNota_DevuelveVacio()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new FacturaCompra { Entry = 7, DocSubType = "DN" });
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle> { new() { Entry = 7, NoLinea = 1 } }.AsAsyncQueryable());

            Assert.Empty(await _domain.ObtenerPorFacturaCompraAsync(7));
        }

        [Fact]
        public async Task ObtenerPorFacturaCompraAsync_DeUnaFactura_DevuelveSusLineas()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new FacturaCompra { Entry = 7, DocSubType = "--" });
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>
            {
                new() { Entry = 7, NoLinea = 1 }, new() { Entry = 8, NoLinea = 1 }
            }.AsAsyncQueryable());

            var lineas = (await _domain.ObtenerPorFacturaCompraAsync(7)).ToList();

            Assert.Equal(new[] { 1 }, lineas.Select(l => l.NoLinea));
        }

        [Fact]
        public async Task ObtenerAsync_DeLineaDeNota_DevuelveNull()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new FacturaCompra { Entry = 7, DocSubType = "DN" });
            _repoDet.Setup(r => r.ObtenerAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 7 && k.NoLinea == 1))).ReturnsAsync(new FacturaCompraDetalle { Entry = 7, NoLinea = 1 });

            Assert.Null(await _domain.ObtenerAsync(7, 1));
        }

        [Fact]
        public async Task ObtenerTodoAsync_ExcluyeLineasDeNotas()
        {
            _repoHeader.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>
            {
                new() { Entry = 7, DocSubType = "--" }, new() { Entry = 8, DocSubType = "DN" }
            }.AsAsyncQueryable());
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>
            {
                new() { Entry = 7, NoLinea = 1 }, new() { Entry = 8, NoLinea = 1 }, new() { Entry = 8, NoLinea = 2 }
            }.AsAsyncQueryable());

            var lineas = (await _domain.ObtenerTodoAsync()).ToList();

            Assert.Equal(new[] { 7 }, lineas.Select(l => l.Entry));
        }
    }
}
