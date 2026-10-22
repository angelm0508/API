using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class OrdenServicioLineaServiceTests
    {
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArticulo = new();
        private readonly Mock<IRepositorioGenerico<Impuesto, string>> _repoImpuesto = new();
        private readonly Mock<IRepositorioGenerico<Mecanico, string>> _repoMecanico = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly OrdenServicioLineaService _svc;
        private readonly List<MovimientoRequest> _movimientos = new();

        public OrdenServicioLineaServiceTests()
        {
            _svc = new OrdenServicioLineaService(_repoArticulo.Object, _repoImpuesto.Object, _repoMecanico.Object, _asiento.Object);

            // Valores por defecto válidos: cada prueba rompe solo lo que quiere probar.
            _repoArticulo.Setup(r => r.ObtenerAsync("ACE-1")).ReturnsAsync(new Articulo
            {
                Codigo = "ACE-1", Nombre = "Aceite 20W50", Activo = "S", ArticuloVenta = "S", ArticuloInventario = "S", AlmacenDefecto = "01"
            });
            _repoArticulo.Setup(r => r.ObtenerAsync("MO-1")).ReturnsAsync(new Articulo
            {
                Codigo = "MO-1", Nombre = "Mano de obra", Activo = "S", ArticuloVenta = "S", ArticuloInventario = "N"
            });
            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA", Tasa = 12m });
            _repoMecanico.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Juan", Activo = "S" });
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((ms, _) => _movimientos.AddRange(ms))
                .Returns(Task.CompletedTask);
        }

        private static OrdenServicioDetalle Insumo(Action<OrdenServicioDetalle>? ajustar = null)
        {
            var l = new OrdenServicioDetalle { CodArticulo = "ACE-1", Cantidad = 2m, Precio = 100m, PrctjeDesc = 10m, CodigoImpuesto = "IVA" };
            ajustar?.Invoke(l);
            return l;
        }

        private static OrdenServicioDetalle Servicio(Action<OrdenServicioDetalle>? ajustar = null)
        {
            var l = new OrdenServicioDetalle { CodArticulo = "MO-1", Cantidad = 1m, Precio = 50m, CodigoImpuesto = "IVA" };
            ajustar?.Invoke(l);
            return l;
        }

        private async Task<string> MensajeAlPreparar(OrdenServicioDetalle linea, int numeroInicial = 1)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.PrepararAsync(new List<OrdenServicioDetalle> { linea }, numeroInicial));
            return ex.Message;
        }

        // ---------------- PrepararAsync: camino feliz ----------------

        [Fact]
        public async Task Preparar_Insumo_CompletaDatosYCalculaMontosEnElServidor()
        {
            var linea = Insumo(l => { l.Impuesto = 999m; l.TotalLinea = 999m; });   // lo que mande el cliente se ignora

            await _svc.PrepararAsync(new List<OrdenServicioDetalle> { linea });

            Assert.Equal("S", linea.EsInsumo);
            Assert.Equal("70", linea.TipoObjeto);
            Assert.Equal("01", linea.CodAlmacen);                 // almacén por defecto del artículo
            Assert.Equal("Aceite 20W50", linea.Descripcion);      // descripción por defecto = nombre del artículo
            // 2 x 100 = 200; desc 10% = 20; base 180; IVA 12% = 21.60; total 201.60
            Assert.Equal(21.60m, linea.Impuesto);
            Assert.Equal(201.60m, linea.TotalLinea);
        }

        [Fact]
        public async Task Preparar_Insumo_ConservaAlmacenYDescripcionIndicados()
        {
            var linea = Insumo(l => { l.CodAlmacen = " 02 "; l.Descripcion = "  Aceite sintético "; });

            await _svc.PrepararAsync(new List<OrdenServicioDetalle> { linea });

            Assert.Equal("02", linea.CodAlmacen);
            Assert.Equal("Aceite sintético", linea.Descripcion);
        }

        [Fact]
        public async Task Preparar_Servicio_NoEsInsumoYNoExigeAlmacen()
        {
            var linea = Servicio();

            await _svc.PrepararAsync(new List<OrdenServicioDetalle> { linea });

            Assert.Equal("N", linea.EsInsumo);
            Assert.Null(linea.CodAlmacen);
            Assert.Equal(0m, linea.PrctjeDesc);
            Assert.Equal(6.00m, linea.Impuesto);
            Assert.Equal(56.00m, linea.TotalLinea);
        }

        [Fact]
        public async Task Preparar_ServicioConAlmacenPorDefectoEnElArticulo_LoCopia()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("MO-2")).ReturnsAsync(new Articulo
            {
                Codigo = "MO-2", Nombre = "Afinación", Activo = "S", ArticuloVenta = "S", ArticuloInventario = "N", AlmacenDefecto = "01"
            });
            var linea = Servicio(l => l.CodArticulo = "MO-2");

            await _svc.PrepararAsync(new List<OrdenServicioDetalle> { linea });

            Assert.Equal("N", linea.EsInsumo);
            Assert.Equal("01", linea.CodAlmacen);
        }

        [Fact]
        public async Task Preparar_ConMecanicoValido_Pasa()
        {
            await _svc.PrepararAsync(new List<OrdenServicioDetalle> { Servicio(l => l.CodMecanico = "M01") });
        }

        // ---------------- PrepararAsync: validaciones ----------------

        [Fact]
        public async Task Preparar_ArticuloVacio_Lanza()
        {
            Assert.Equal("Línea 1: el artículo es requerido.", await MensajeAlPreparar(Insumo(l => l.CodArticulo = "  ")));
        }

        [Fact]
        public async Task Preparar_ArticuloInexistente_Lanza()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("XX")).ReturnsAsync((Articulo?)null);

            Assert.Equal("Línea 1: el artículo 'XX' no existe.", await MensajeAlPreparar(Insumo(l => l.CodArticulo = "XX")));
        }

        [Fact]
        public async Task Preparar_ArticuloInactivo_Lanza()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("OLD")).ReturnsAsync(new Articulo { Codigo = "OLD", Activo = "N", ArticuloVenta = "S", ArticuloInventario = "S" });

            Assert.Equal("Línea 1: el artículo 'OLD' está inactivo.", await MensajeAlPreparar(Insumo(l => l.CodArticulo = "OLD")));
        }

        [Fact]
        public async Task Preparar_ArticuloNoVendible_Lanza()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("COMP")).ReturnsAsync(new Articulo { Codigo = "COMP", Activo = "S", ArticuloVenta = "N", ArticuloInventario = "S" });

            Assert.Equal("Línea 1: el artículo 'COMP' no está habilitado para la venta.", await MensajeAlPreparar(Insumo(l => l.CodArticulo = "COMP")));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Preparar_CantidadNoPositiva_Lanza(double cantidad)
        {
            Assert.Equal("Línea 1: la cantidad debe ser mayor que 0.", await MensajeAlPreparar(Insumo(l => l.Cantidad = (decimal)cantidad)));
        }

        [Fact]
        public async Task Preparar_CantidadNula_Lanza()
        {
            Assert.Equal("Línea 1: la cantidad debe ser mayor que 0.", await MensajeAlPreparar(Insumo(l => l.Cantidad = null)));
        }

        [Fact]
        public async Task Preparar_PrecioNulo_Lanza()
        {
            Assert.Equal("Línea 1: el precio es requerido.", await MensajeAlPreparar(Insumo(l => l.Precio = null)));
        }

        [Fact]
        public async Task Preparar_PrecioNegativo_Lanza()
        {
            Assert.Equal("Línea 1: el precio no puede ser negativo.", await MensajeAlPreparar(Insumo(l => l.Precio = -1m)));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        public async Task Preparar_DescuentoFueraDeRango_Lanza(double descuento)
        {
            Assert.Equal("Línea 1: el descuento debe estar entre 0 y 100.", await MensajeAlPreparar(Insumo(l => l.PrctjeDesc = (decimal)descuento)));
        }

        [Fact]
        public async Task Preparar_ImpuestoVacio_Lanza()
        {
            Assert.Equal("Línea 1: el impuesto es requerido.", await MensajeAlPreparar(Insumo(l => l.CodigoImpuesto = " ")));
        }

        [Fact]
        public async Task Preparar_ImpuestoInexistente_Lanza()
        {
            _repoImpuesto.Setup(r => r.ObtenerAsync("ZZ")).ReturnsAsync((Impuesto?)null);

            Assert.Equal("Línea 1: el impuesto 'ZZ' no existe.", await MensajeAlPreparar(Insumo(l => l.CodigoImpuesto = "ZZ")));
        }

        [Fact]
        public async Task Preparar_InsumoSinAlmacenNiAlmacenPorDefecto_Lanza()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("SIN")).ReturnsAsync(new Articulo
            {
                Codigo = "SIN", Nombre = "Sin almacén", Activo = "S", ArticuloVenta = "S", ArticuloInventario = "S", AlmacenDefecto = null
            });

            Assert.Equal("Línea 1: el almacén es requerido para un artículo de inventario.", await MensajeAlPreparar(Insumo(l => l.CodArticulo = "SIN")));
        }

        [Fact]
        public async Task Preparar_DescripcionLarga_Lanza()
        {
            Assert.Equal("Línea 1: la descripción no puede superar 200 caracteres.", await MensajeAlPreparar(Insumo(l => l.Descripcion = new string('x', 201))));
        }

        [Fact]
        public async Task Preparar_MecanicoInexistente_Lanza()
        {
            _repoMecanico.Setup(r => r.ObtenerAsync("M99")).ReturnsAsync((Mecanico?)null);

            Assert.Equal("Línea 1: el mecánico 'M99' no existe.", await MensajeAlPreparar(Servicio(l => l.CodMecanico = "M99")));
        }

        [Fact]
        public async Task Preparar_MecanicoInactivo_Lanza()
        {
            _repoMecanico.Setup(r => r.ObtenerAsync("M02")).ReturnsAsync(new Mecanico { Codigo = "M02", Nombre = "Pedro", Activo = "N" });

            Assert.Equal("Línea 1: el mecánico 'M02' está inactivo.", await MensajeAlPreparar(Servicio(l => l.CodMecanico = "M02")));
        }

        [Fact]
        public async Task Preparar_NumeroInicial_AjustaElNumeroDeLineaEnLosMensajes()
        {
            var lineas = new List<OrdenServicioDetalle> { Servicio(), Insumo(l => l.Cantidad = 0m) };

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.PrepararAsync(lineas, numeroInicial: 4));

            Assert.Equal("Línea 5: la cantidad debe ser mayor que 0.", ex.Message);
        }

        // ---------------- RecalcularCabecera ----------------

        [Fact]
        public async Task RecalcularCabecera_SumaLasLineas()
        {
            var lineas = new List<OrdenServicioDetalle> { Insumo(), Servicio() };
            await _svc.PrepararAsync(lineas);
            var orden = new OrdenServicio();

            _svc.RecalcularCabecera(orden, lineas);

            // bruto 200 + 50; descuento 20 + 0; impuesto 21.60 + 6.00; total 201.60 + 56.00
            Assert.Equal(250m, orden.TotalBruto);
            Assert.Equal(20m, orden.TotalDesc);
            Assert.Equal(27.60m, orden.TotalImp);
            Assert.Equal(257.60m, orden.TotalDoc);
        }

        [Fact]
        public void RecalcularCabecera_SinLineas_DejaTodoEnCero()
        {
            var orden = new OrdenServicio { TotalBruto = 5m, TotalDesc = 5m, TotalImp = 5m, TotalDoc = 5m };

            _svc.RecalcularCabecera(orden, new List<OrdenServicioDetalle>());

            Assert.Equal((0m, 0m, 0m, 0m), (orden.TotalBruto, orden.TotalDesc, orden.TotalImp, orden.TotalDoc));
        }

        // ---------------- AsentarSalidaAsync ----------------

        [Fact]
        public async Task AsentarSalida_SoloLosInsumosConCantidad_ComoSalidaDeLaOrden()
        {
            var insumo = Insumo(l => { l.Entry = 77; l.NoLinea = 3; l.EsInsumo = "S"; l.CodAlmacen = "01"; });
            var servicio = Servicio(l => { l.Entry = 77; l.NoLinea = 4; l.EsInsumo = "N"; });
            var fecha = new DateTime(2026, 10, 22);

            await _svc.AsentarSalidaAsync(77, fecha, new[] { insumo, servicio });

            var m = Assert.Single(_movimientos);
            Assert.Equal(new MovimientoRequest("70", 77, 3, "ACE-1", "01", -2m, 100m, fecha), m);
            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), false), Times.Once);
        }

        [Fact]
        public async Task AsentarSalida_SoloServicios_NoLlamaAlInventario()
        {
            await _svc.AsentarSalidaAsync(77, DateTime.Now, new[] { Servicio(l => { l.NoLinea = 1; l.EsInsumo = "N"; }) });

            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()), Times.Never);
        }
    }
}
