using API.Domain.Core;
using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class OrdenServicioDetalleDomainTests
    {
        private readonly Mock<IRepositorioGenerico<OrdenServicio, int>> _repoOrden = new();
        private readonly Mock<IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IOrdenServicioLineaService> _lineas = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly List<OrdenServicioDetalle> _guardadas = new();
        private readonly List<string> _secuencia = new();
        private readonly OrdenServicioDetalleDomain _domain;

        public OrdenServicioDetalleDomainTests()
        {
            _domain = new OrdenServicioDetalleDomain(_repoOrden.Object, _repoDetalle.Object, _lineas.Object, _asiento.Object, _documentoBase.Object, _tx.Object);

            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _guardadas.AsAsyncQueryable());
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<OrdenServicioDetalle>())).Returns(Task.CompletedTask);
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).ReturnsAsync(true);
            _repoDetalle.Setup(r => r.ObtenerAsync(It.IsAny<(int Entry, int NoLinea)>()))
                .ReturnsAsync(((int Entry, int NoLinea) k) => _guardadas.FirstOrDefault(l => l.Entry == k.Entry && l.NoLinea == k.NoLinea));
        }

        private OrdenServicio Orden(string estado = "R", string cancelado = "N")
        {
            var o = new OrdenServicio { Entry = 50, EstadoServicio = estado, Cancelado = cancelado };
            _repoOrden.Setup(r => r.ObtenerAsync(50)).ReturnsAsync(o);
            return o;
        }

        private OrdenServicioDetalle Guardada(int noLinea, string art = "ACE-1", decimal cant = 2m, string esInsumo = "S", string? alm = "01")
        {
            var l = new OrdenServicioDetalle
            {
                Entry = 50, NoLinea = noLinea, CodArticulo = art, Cantidad = cant, Precio = 100m, EsInsumo = esInsumo, CodAlmacen = alm, CodigoImpuesto = "IVA"
            };
            _guardadas.Add(l);
            return l;
        }

        private static OrdenServicioDetalle Nueva(Action<OrdenServicioDetalle>? ajustar = null)
        {
            var l = new OrdenServicioDetalle { Entry = 50, CodArticulo = "ACE-1", Cantidad = 1m, Precio = 100m, CodigoImpuesto = "IVA" };
            ajustar?.Invoke(l);
            return l;
        }

        // ---------------- InsertarAsync ----------------

        [Fact]
        public async Task InsertarAsync_PrimeraLinea_AsignaNumero1_AsientaYRecalculaConTodasLasLineas()
        {
            var orden = Orden();
            var nueva = Nueva();

            var noLinea = await _domain.InsertarAsync(nueva);

            Assert.Equal(1, noLinea);
            Assert.Equal(1, nueva.NoLinea);
            _lineas.Verify(l => l.PrepararAsync(It.Is<IList<OrdenServicioDetalle>>(x => x.Count == 1 && x[0] == nueva), 1), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(nueva), Times.Once);
            _lineas.Verify(l => l.AsentarSalidaAsync(50, It.IsAny<DateTime>(), It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Single() == nueva)), Times.Once);
            _lineas.Verify(l => l.RecalcularCabecera(orden, It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Count() == 1)), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_ConLineasPrevias_UsaElSiguienteNumeroAunqueHayaHuecos()
        {
            var orden = Orden();
            Guardada(1);
            Guardada(3);
            var nueva = Nueva();

            var noLinea = await _domain.InsertarAsync(nueva);

            Assert.Equal(4, noLinea);
            _lineas.Verify(l => l.PrepararAsync(It.IsAny<IList<OrdenServicioDetalle>>(), 4), Times.Once);
            _lineas.Verify(l => l.RecalcularCabecera(orden, It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Count() == 3)), Times.Once);
        }

        [Theory]
        [InlineData("R")]
        [InlineData("P")]
        [InlineData("T")]
        public async Task InsertarAsync_EnEstadosEditables_Permite(string estado)
        {
            Orden(estado);

            Assert.Equal(1, await _domain.InsertarAsync(Nueva()));
        }

        [Fact]
        public async Task InsertarAsync_OrdenInexistente_Lanza()
        {
            _repoOrden.Setup(r => r.ObtenerAsync(77)).ReturnsAsync((OrdenServicio?)null);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nueva(l => l.Entry = 77)));
            Assert.Equal("La orden de servicio no existe.", ex.Message);
        }

        [Fact]
        public async Task InsertarAsync_OrdenCancelada_Lanza()
        {
            Orden(cancelado: "S");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nueva()));
            Assert.Equal("La orden de servicio está cancelada y no se puede modificar.", ex.Message);
        }

        [Fact]
        public async Task InsertarAsync_OrdenEntregada_Lanza()
        {
            Orden("E");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nueva()));
            Assert.Equal("La orden de servicio ya fue entregada: solo se puede modificar el comentario.", ex.Message);
        }

        [Fact]
        public async Task InsertarAsync_LineaInvalida_NoEntraALaTransaccion()
        {
            Orden();
            _lineas.Setup(l => l.PrepararAsync(It.IsAny<IList<OrdenServicioDetalle>>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Línea 1: la cantidad debe ser mayor que 0."));

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nueva()));

            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<OrdenServicioDetalle>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_StockInsuficiente_Propaga()
        {
            Orden();
            _lineas.Setup(l => l.AsentarSalidaAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<OrdenServicioDetalle>>()))
                .ThrowsAsync(new StockInsuficienteException("ACE-1", "01", 0m, 1m));

            await Assert.ThrowsAsync<StockInsuficienteException>(() => _domain.InsertarAsync(Nueva()));
        }

        // ---------------- ActualizarAsync ----------------

        [Fact]
        public async Task ActualizarAsync_RevierteLaLineaLuegoAsientaLaNuevaYRecalcula()
        {
            var orden = Orden();
            var existente = Guardada(1, cant: 2m);
            var otra = Guardada(2, art: "MO-1", esInsumo: "N", alm: null);
            _asiento.Setup(a => a.RevertirLineaAsync("70", 50, 1)).Callback(() => _secuencia.Add("revertir")).Returns(Task.CompletedTask);
            _lineas.Setup(l => l.AsentarSalidaAsync(50, It.IsAny<DateTime>(), It.IsAny<IEnumerable<OrdenServicioDetalle>>()))
                .Callback(() => _secuencia.Add("asentar")).Returns(Task.CompletedTask);
            _lineas.Setup(l => l.PrepararAsync(It.IsAny<IList<OrdenServicioDetalle>>(), It.IsAny<int>()))
                .Callback<IList<OrdenServicioDetalle>, int>((lista, _) =>
                {
                    lista[0].EsInsumo = "S";
                    lista[0].CodAlmacen = "01";
                    lista[0].Impuesto = 36m;
                    lista[0].TotalLinea = 336m;
                    lista[0].TipoObjeto = "70";
                }).Returns(Task.CompletedTask);

            var ok = await _domain.ActualizarAsync(50, 1, Nueva(l => { l.Cantidad = 3m; l.Precio = 100m; l.Descripcion = "Aceite"; }));

            Assert.True(ok);
            Assert.Equal(new[] { "revertir", "asentar" }, _secuencia);
            Assert.Equal((3m, 100m, "Aceite", 36m, 336m, "S", "01"),
                (existente.Cantidad, existente.Precio, existente.Descripcion, existente.Impuesto, existente.TotalLinea, existente.EsInsumo, existente.CodAlmacen));
            _lineas.Verify(l => l.PrepararAsync(It.IsAny<IList<OrdenServicioDetalle>>(), 1), Times.Once);
            _lineas.Verify(l => l.AsentarSalidaAsync(50, It.IsAny<DateTime>(), It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Single() == existente)), Times.Once);
            _lineas.Verify(l => l.RecalcularCabecera(orden, It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Count() == 2 && x.Contains(existente) && x.Contains(otra))), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_ValidaQueLaLineaNoEsteFacturada()
        {
            Orden();
            Guardada(1);
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.OrdenServicio, 50, 1, "modificar"))
                .ThrowsAsync(new Exception("No se puede modificar la línea 1: la usan Factura 2. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(50, 1, Nueva()));

            Assert.Equal("No se puede modificar la línea 1: la usan Factura 2. Cancele primero esos documentos.", ex.Message);
            _asiento.Verify(a => a.RevertirLineaAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_LineaInexistente_DevuelveFalso()
        {
            Orden();

            Assert.False(await _domain.ActualizarAsync(50, 9, Nueva()));
        }

        [Fact]
        public async Task ActualizarAsync_OrdenEntregada_Lanza()
        {
            Orden("E");
            Guardada(1);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(50, 1, Nueva()));
            Assert.Equal("La orden de servicio ya fue entregada: solo se puede modificar el comentario.", ex.Message);
        }

        [Fact]
        public async Task ActualizarAsync_LineaInvalida_NoRevierteElInventario()
        {
            Orden();
            Guardada(1);
            _lineas.Setup(l => l.PrepararAsync(It.IsAny<IList<OrdenServicioDetalle>>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Línea 1: el artículo 'XX' no existe."));

            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(50, 1, Nueva(l => l.CodArticulo = "XX")));

            _asiento.Verify(a => a.RevertirLineaAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_StockInsuficienteAlReasentar_Propaga()
        {
            Orden();
            Guardada(1);
            _lineas.Setup(l => l.AsentarSalidaAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<OrdenServicioDetalle>>()))
                .ThrowsAsync(new StockInsuficienteException("ACE-1", "01", 1m, 9m));

            await Assert.ThrowsAsync<StockInsuficienteException>(() => _domain.ActualizarAsync(50, 1, Nueva(l => l.Cantidad = 9m)));
        }

        // ---------------- EliminarAsync ----------------

        [Fact]
        public async Task EliminarAsync_RevierteLaLineaLaBorraYRecalculaConLasRestantes()
        {
            var orden = Orden();
            Guardada(1);
            var restante = Guardada(2, art: "MO-1", esInsumo: "N", alm: null);
            _asiento.Setup(a => a.RevertirLineaAsync("70", 50, 1)).Callback(() => _secuencia.Add("revertir")).Returns(Task.CompletedTask);
            _repoDetalle.Setup(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 50 && k.NoLinea == 1)))
                .Callback(() => _secuencia.Add("borrar")).ReturnsAsync(true);

            Assert.True(await _domain.EliminarAsync(50, 1));

            Assert.Equal(new[] { "revertir", "borrar" }, _secuencia);
            _lineas.Verify(l => l.RecalcularCabecera(orden, It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Single() == restante)), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_ValidaQueLaLineaNoEsteFacturada()
        {
            Orden();
            Guardada(1);
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.OrdenServicio, 50, 1, "eliminar"))
                .ThrowsAsync(new Exception("No se puede eliminar la línea 1: la usan Factura 2. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(50, 1));

            Assert.Equal("No se puede eliminar la línea 1: la usan Factura 2. Cancele primero esos documentos.", ex.Message);
            _asiento.Verify(a => a.RevertirLineaAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _repoDetalle.Verify(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_LineaInexistente_DevuelveFalso()
        {
            Orden();

            Assert.False(await _domain.EliminarAsync(50, 9));
        }

        [Fact]
        public async Task EliminarAsync_OrdenCancelada_Lanza()
        {
            Orden(cancelado: "S");
            Guardada(1);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(50, 1));
            Assert.Equal("La orden de servicio está cancelada y no se puede modificar.", ex.Message);
        }
    }
}
