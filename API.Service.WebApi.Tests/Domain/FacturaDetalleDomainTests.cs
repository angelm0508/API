using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class FacturaDetalleDomainTests
    {
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _repoDet = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoHeader = new();
        private readonly FacturaDetalleDomain _domain;

        public FacturaDetalleDomainTests()
        {
            _domain = new FacturaDetalleDomain(_repoDet.Object, _repoHeader.Object);
        }

        [Fact]
        public async Task InsertarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new FacturaDetalle { Entry = 7 }));
            _repoDet.Verify(r => r.InsertarAsync(It.IsAny<FacturaDetalle>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_DocumentoNoExiste_Lanza()
        {
            // Sin encabezado sembrado: el insert suelto de líneas se rechaza siempre
            // (sin FK a Factura, un Entry inexistente generaría una línea huérfana).
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new FacturaDetalle { Entry = 7 }));
            _repoDet.Verify(r => r.InsertarAsync(It.IsAny<FacturaDetalle>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(7, 1, new FacturaDetalle()));
        }

        [Fact]
        public async Task EliminarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(7, 1));
        }

        // ---------------- Las líneas de notas de débito no se exponen por este recurso ----------------

        [Fact]
        public async Task ObtenerPorFacturaAsync_DeUnaNota_DevuelveVacio()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7, DocSubType = "ND" });
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaDetalle> { new() { Entry = 7, NoLinea = 1 } }.AsAsyncQueryable());

            Assert.Empty(await _domain.ObtenerPorFacturaAsync(7));
        }

        [Fact]
        public async Task ObtenerPorFacturaAsync_DeUnaFactura_DevuelveSusLineas()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7, DocSubType = "--" });
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaDetalle>
            {
                new() { Entry = 7, NoLinea = 1 }, new() { Entry = 8, NoLinea = 1 }
            }.AsAsyncQueryable());

            var lineas = (await _domain.ObtenerPorFacturaAsync(7)).ToList();

            Assert.Equal(new[] { 1 }, lineas.Select(l => l.NoLinea));
        }

        [Fact]
        public async Task ObtenerAsync_DeLineaDeNota_DevuelveNull()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7, DocSubType = "ND" });
            _repoDet.Setup(r => r.ObtenerAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 7 && k.NoLinea == 1))).ReturnsAsync(new FacturaDetalle { Entry = 7, NoLinea = 1 });

            Assert.Null(await _domain.ObtenerAsync(7, 1));
        }

        [Fact]
        public async Task ObtenerTodoAsync_ExcluyeLineasDeNotas()
        {
            _repoHeader.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<Factura>
            {
                new() { Entry = 7, DocSubType = "--" }, new() { Entry = 8, DocSubType = "ND" }
            }.AsAsyncQueryable());
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaDetalle>
            {
                new() { Entry = 7, NoLinea = 1 }, new() { Entry = 8, NoLinea = 1 }, new() { Entry = 8, NoLinea = 2 }
            }.AsAsyncQueryable());

            var lineas = (await _domain.ObtenerTodoAsync()).ToList();

            Assert.Equal(new[] { 7 }, lineas.Select(l => l.Entry));
        }
    }
}
