using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PedidoDetalleDomainTests
    {
        private readonly Mock<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>> _repoDet = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly PedidoDetalleDomain _domain;

        public PedidoDetalleDomainTests()
        {
            _domain = new PedidoDetalleDomain(_repoDet.Object, _documentoBase.Object);
        }

        [Fact]
        public async Task ActualizarAsync_LineaSinDerivados_Actualiza()
        {
            _repoDet.Setup(r => r.ActualizarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 5 && k.NoLinea == 2), It.IsAny<PedidoDetalle>())).ReturnsAsync(true);

            var ok = await _domain.ActualizarAsync(5, 2, new PedidoDetalle { Entry = 5, NoLinea = 2 });

            Assert.True(ok);
            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Pedido, 5, 2, "modificar"), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_LineaConsumida_Lanza_YNoActualiza()
        {
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Pedido, 5, 2, "modificar"))
                .ThrowsAsync(new Exception("No se puede modificar la línea 2: la usan Entrega 3. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, 2, new PedidoDetalle()));

            Assert.Equal("No se puede modificar la línea 2: la usan Entrega 3. Cancele primero esos documentos.", ex.Message);
            _repoDet.Verify(r => r.ActualizarAsync(It.IsAny<(int, int)>(), It.IsAny<PedidoDetalle>()), Times.Never);
        }

        // ---- I-1: la referencia al documento base de una línea guardada es inmutable ----

        private static PedidoDetalle LineaCopiada() => new()
        {
            Entry = 5, NoLinea = 2, BaseTipo = 3, BaseEntry = 12, BaseLinea = 1,
            TipoDocDestino = 5, DocDestinoEntry = 9, BaseRef = 77,
            CodArticulo = "ART1", Cantidad = 10m, Precio = 100m
        };

        private void ConLineaGuardada(PedidoDetalle guardada)
        {
            _repoDet.Setup(r => r.ObtenerAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == guardada.Entry && k.NoLinea == guardada.NoLinea))).ReturnsAsync(guardada);
            _repoDet.Setup(r => r.ActualizarAsync(It.IsAny<(int Entry, int NoLinea)>(), It.IsAny<PedidoDetalle>())).ReturnsAsync(true);
        }

        [Fact]
        public async Task ActualizarAsync_LineaCopiada_ConservaLaReferenciaBaseGuardada()
        {
            ConLineaGuardada(LineaCopiada());
            // El panel de edición de la Web no envía campos base: llegan nulos.
            var entrante = new PedidoDetalle { CodArticulo = "ART1", Cantidad = 10m, Precio = 120m };

            await _domain.ActualizarAsync(5, 2, entrante);

            Assert.Equal((3, 12, 1), (entrante.BaseTipo, entrante.BaseEntry, entrante.BaseLinea));
            Assert.Equal((5, 9, 77), (entrante.TipoDocDestino, entrante.DocDestinoEntry, entrante.BaseRef));
            _repoDet.Verify(r => r.ActualizarAsync(It.IsAny<(int, int)>(), entrante), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_LineaCopiada_IgnoraCamposBaseFalsosDelCliente()
        {
            ConLineaGuardada(LineaCopiada());
            var entrante = new PedidoDetalle { CodArticulo = "ART1", Cantidad = 10m, BaseTipo = 3, BaseEntry = 999, BaseLinea = 8, TipoDocDestino = 6, DocDestinoEntry = 1, BaseRef = 1 };

            await _domain.ActualizarAsync(5, 2, entrante);

            Assert.Equal((3, 12, 1), (entrante.BaseTipo, entrante.BaseEntry, entrante.BaseLinea));
            Assert.Equal((5, 9, 77), (entrante.TipoDocDestino, entrante.DocDestinoEntry, entrante.BaseRef));
        }

        [Fact]
        public async Task ActualizarAsync_LineaSinBase_IgnoraCamposBaseAjenos()
        {
            ConLineaGuardada(new PedidoDetalle { Entry = 5, NoLinea = 2, BaseTipo = -1, CodArticulo = "ART1", Cantidad = 10m });
            var entrante = new PedidoDetalle { CodArticulo = "ART2", Cantidad = 50m, BaseTipo = 3, BaseEntry = 12, BaseLinea = 1 };

            await _domain.ActualizarAsync(5, 2, entrante);

            Assert.Equal(-1, entrante.BaseTipo);
            Assert.Null(entrante.BaseEntry);
            Assert.Null(entrante.BaseLinea);
        }

        [Fact]
        public async Task ActualizarAsync_LineaCopiada_CambiarElArticulo_Lanza_YNoActualiza()
        {
            ConLineaGuardada(LineaCopiada());

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, 2, new PedidoDetalle { CodArticulo = "ART2", Cantidad = 10m }));

            Assert.Equal("El artículo de una línea copiada no se puede cambiar.", ex.Message);
            _repoDet.Verify(r => r.ActualizarAsync(It.IsAny<(int, int)>(), It.IsAny<PedidoDetalle>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_LineaCopiada_AumentarLaCantidad_Lanza_YNoActualiza()
        {
            ConLineaGuardada(LineaCopiada());

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, 2, new PedidoDetalle { CodArticulo = "ART1", Cantidad = 10.5m }));

            Assert.Equal("La cantidad de una línea copiada no puede aumentar. Cree otro documento con lo que falta.", ex.Message);
            _repoDet.Verify(r => r.ActualizarAsync(It.IsAny<(int, int)>(), It.IsAny<PedidoDetalle>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_LineaCopiada_BajarCantidadYCambiarOtrosCampos_Actualiza()
        {
            ConLineaGuardada(LineaCopiada());
            var entrante = new PedidoDetalle { CodArticulo = "ART1", Cantidad = 4m, Precio = 55m, PrctjeDesc = 5m, CodAlmacen = "ALM2", CodigoImpuesto = "IVA", Descripcion = "otra" };

            Assert.True(await _domain.ActualizarAsync(5, 2, entrante));
        }

        [Fact]
        public async Task ActualizarAsync_LineaCopiada_MismoArticuloYMismaCantidad_Actualiza()
        {
            ConLineaGuardada(LineaCopiada());

            Assert.True(await _domain.ActualizarAsync(5, 2, new PedidoDetalle { CodArticulo = "ART1", Cantidad = 10m, Precio = 90m }));
        }

        [Fact]
        public async Task ActualizarAsync_LineaSinBase_PuedeCambiarArticuloYSubirCantidad()
        {
            ConLineaGuardada(new PedidoDetalle { Entry = 5, NoLinea = 2, BaseTipo = -1, CodArticulo = "ART1", Cantidad = 10m });

            Assert.True(await _domain.ActualizarAsync(5, 2, new PedidoDetalle { CodArticulo = "ART2", Cantidad = 50m }));
        }

        // ---- I-2c: las líneas copiadas solo se definen al crear el documento ----

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public async Task InsertarAsync_LineaConBase_Lanza_YNoInserta(int baseTipo)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new PedidoDetalle { Entry = 5, BaseTipo = baseTipo, BaseEntry = 12, BaseLinea = 1, Cantidad = 1m }));

            Assert.Equal("Las líneas copiadas se definen al crear el documento.", ex.Message);
            _repoDet.Verify(r => r.InsertarAsync(It.IsAny<PedidoDetalle>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_LineaConBaseTipoMenosUno_SeInserta()
        {
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoDetalle>().AsAsyncQueryable());
            _repoDet.Setup(r => r.InsertarAsync(It.IsAny<PedidoDetalle>())).ReturnsAsync((PedidoDetalle l) => l);

            var noLinea = await _domain.InsertarAsync(new PedidoDetalle { Entry = 5, BaseTipo = -1 });

            Assert.Equal(1, noLinea);
            _repoDet.Verify(r => r.InsertarAsync(It.IsAny<PedidoDetalle>()), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_LineaSinDerivados_Elimina()
        {
            _repoDet.Setup(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 5 && k.NoLinea == 2))).ReturnsAsync(true);

            Assert.True(await _domain.EliminarAsync(5, 2));
            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Pedido, 5, 2, "eliminar"), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_LineaConsumida_Lanza_YNoElimina()
        {
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Pedido, 5, 2, "eliminar"))
                .ThrowsAsync(new Exception("No se puede eliminar la línea 2: la usan Entrega 3. Cancele primero esos documentos."));

            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(5, 2));

            _repoDet.Verify(r => r.EliminarAsync(It.IsAny<(int, int)>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_AgregarLineaNueva_NoSeBloquea()
        {
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoDetalle>().AsAsyncQueryable());
            _repoDet.Setup(r => r.InsertarAsync(It.IsAny<PedidoDetalle>())).ReturnsAsync((PedidoDetalle l) => l);

            await _domain.InsertarAsync(new PedidoDetalle { Entry = 5 });

            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }
    }
}