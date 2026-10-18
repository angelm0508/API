using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class CotizacionDetalleDomainTests
    {
        private readonly Mock<IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)>> _repoDet = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly CotizacionDetalleDomain _domain;

        public CotizacionDetalleDomainTests()
        {
            _domain = new CotizacionDetalleDomain(_repoDet.Object, _documentoBase.Object);
        }

        [Fact]
        public async Task ActualizarAsync_LineaSinDerivados_Actualiza()
        {
            _repoDet.Setup(r => r.ActualizarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 5 && k.NoLinea == 2), It.IsAny<CotizacionDetalle>())).ReturnsAsync(true);

            var ok = await _domain.ActualizarAsync(5, 2, new CotizacionDetalle { Entry = 5, NoLinea = 2 });

            Assert.True(ok);
            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, 5, 2, "modificar"), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_LineaConsumida_Lanza_YNoActualiza()
        {
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, 5, 2, "modificar"))
                .ThrowsAsync(new Exception("No se puede modificar la línea 2: la usan Pedido 3. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, 2, new CotizacionDetalle()));

            Assert.Equal("No se puede modificar la línea 2: la usan Pedido 3. Cancele primero esos documentos.", ex.Message);
            _repoDet.Verify(r => r.ActualizarAsync(It.IsAny<(int, int)>(), It.IsAny<CotizacionDetalle>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_LineaSinDerivados_Elimina()
        {
            _repoDet.Setup(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 5 && k.NoLinea == 2))).ReturnsAsync(true);

            Assert.True(await _domain.EliminarAsync(5, 2));
            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, 5, 2, "eliminar"), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_LineaConsumida_Lanza_YNoElimina()
        {
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, 5, 2, "eliminar"))
                .ThrowsAsync(new Exception("No se puede eliminar la línea 2: la usan Pedido 3. Cancele primero esos documentos."));

            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(5, 2));

            _repoDet.Verify(r => r.EliminarAsync(It.IsAny<(int, int)>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_AgregarLineaNueva_NoSeBloquea()
        {
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<CotizacionDetalle>().AsAsyncQueryable());
            _repoDet.Setup(r => r.InsertarAsync(It.IsAny<CotizacionDetalle>())).ReturnsAsync((CotizacionDetalle l) => l);

            await _domain.InsertarAsync(new CotizacionDetalle { Entry = 5 });

            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }
    }
}