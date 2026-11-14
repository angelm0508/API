using API.Domain.Core.Reportes.Compartido;
using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class VentasPorArticuloReporteTests
    {
        private static readonly DateTime Enero10 = new(2026, 1, 10);

        private readonly Mock<IRepositorioGenerico<Factura, int>> _facturas = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _detalles = new();
        private readonly Mock<IRepositorioGenerico<MovimientoInventario, int>> _movimientos = new();

        private VentasPorArticuloReporte Crear(IEnumerable<Factura> facturas, IEnumerable<FacturaDetalle> detalles, IEnumerable<MovimientoInventario>? movimientos = null)
        {
            _facturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            _detalles.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(detalles.AsAsyncQueryable());
            _movimientos.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((movimientos ?? Array.Empty<MovimientoInventario>()).AsAsyncQueryable());
            return new VentasPorArticuloReporte(new ConsultaLineasVenta(_facturas.Object, _detalles.Object, _movimientos.Object));
        }

        private static Factura Fac(int entry, DateTime? fecha = null, string cancelado = "N", string subTipo = "--", string sn = "C001",
            string nombreSn = "Cliente Uno", string moneda = "GTQ", int serie = 1, int numDoc = 0) =>
            new()
            {
                Entry = entry, NumDoc = numDoc == 0 ? entry * 10 : numDoc, Serie = serie, Cancelado = cancelado, DocSubType = subTipo,
                FechaDoc = fecha ?? Enero10, CodigoSn = sn, NombreSn = nombreSn, MonedaDoc = moneda
            };

        private static FacturaDetalle Lin(int entry, int noLinea, string art = "A1", decimal cantidad = 2m, decimal venta = 100m,
            string almacen = "01", string? grupo = "Lubricantes") =>
            new()
            {
                Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cantidad, CantidadBase = cantidad,
                TotalLinea = venta + 12m, Impuesto = 12m, CodAlmacen = almacen, PrctjeDesc = 0m,
                CodArticuloNavigation = new Articulo
                {
                    Codigo = art, Nombre = "Artículo " + art,
                    CodigoGrupoNavigation = grupo is null ? null : new GrupoArticulo { Codigo = 100, Nombre = grupo }
                }
            };

        private static MovimientoInventario Mov(int docEntry, int docLinea, decimal sale, decimal valor) =>
            new() { TipoDoc = "6", DocEntry = docEntry, DocLinea = docLinea, CodArticulo = "A1", CodAlmacen = "01", CantidadSale = sale, ValorMovimiento = valor };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor>();
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        [Fact]
        public void Definicion_EsDeVentasConSusFiltrosYColumnas()
        {
            var d = Crear(Array.Empty<Factura>(), Array.Empty<FacturaDetalle>()).Definicion;

            Assert.Equal("ventas-por-articulo", d.Codigo);
            Assert.Equal("Ventas por artículo", d.Nombre);
            Assert.Equal("ReportesVentas", d.Modulo);
            Assert.Equal("Ventas", d.Familia);
            Assert.Equal(new[] { "fechas", "cliente", "articulo", "grupo", "almacen" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal(TipoFiltro.RangoFechas, d.Filtros.Single(f => f.Campo == "fechas").Tipo);
            Assert.Equal("C", d.Filtros.Single(f => f.Campo == "cliente").SubTipo);
            Assert.Equal(TipoFiltro.Articulo, d.Filtros.Single(f => f.Campo == "articulo").Tipo);
            Assert.Equal(TipoFiltro.Texto, d.Filtros.Single(f => f.Campo == "grupo").Tipo);
            Assert.Equal(TipoFiltro.Almacen, d.Filtros.Single(f => f.Campo == "almacen").Tipo);
            Assert.Equal(
                new[] { "fecha", "tipo", "serie", "numDoc", "codigoSn", "nombreSn", "articulo", "nombreArticulo", "grupo", "almacen", "moneda", "cantidad", "precioNeto", "ventaNeta", "costo", "utilidad" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal(TipoColumna.Cantidad, d.Columnas.Single(c => c.Campo == "cantidad").Tipo);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "cantidad").Totaliza);
            Assert.Equal(Totalizacion.Ninguna, d.Columnas.Single(c => c.Campo == "precioNeto").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "ventaNeta").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "costo").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "utilidad").Totaliza);
            Assert.True(d.Columnas.Single(c => c.Campo == "articulo").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "grupo").Agrupable);
            Assert.False(d.Columnas.Single(c => c.Campo == "serie").VisiblePorDefecto);
            Assert.False(d.Columnas.Single(c => c.Campo == "codigoSn").VisiblePorDefecto);
            Assert.False(d.Columnas.Single(c => c.Campo == "moneda").VisiblePorDefecto);
        }

        [Fact]
        public async Task Ejecutar_UnaFilaPorLineaConPrecioNetoYUtilidad()
        {
            var r = Crear(new[] { Fac(1, numDoc: 501) }, new[] { Lin(1, 1, cantidad: 4m, venta: 100m) }, new[] { Mov(1, 1, 4m, -60m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(Enero10, f["fecha"]);
            Assert.Equal("Factura", f["tipo"]);
            Assert.Equal(1, f["serie"]);
            Assert.Equal(501, f["numDoc"]);
            Assert.Equal("C001", f["codigoSn"]);
            Assert.Equal("Cliente Uno", f["nombreSn"]);
            Assert.Equal("A1", f["articulo"]);
            Assert.Equal("Artículo A1", f["nombreArticulo"]);
            Assert.Equal("Lubricantes", f["grupo"]);
            Assert.Equal("01", f["almacen"]);
            Assert.Equal("GTQ", f["moneda"]);
            Assert.Equal(4m, f["cantidad"]);
            Assert.Equal(25m, f["precioNeto"]);
            Assert.Equal(100m, f["ventaNeta"]);
            Assert.Equal(60m, f["costo"]);
            Assert.Equal(40m, f["utilidad"]);
        }

        [Fact]
        public async Task Ejecutar_SinCantidadElPrecioNetoEsCero()
        {
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, cantidad: 0m, venta: 50m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(0m, f["precioNeto"]);
            Assert.Equal(50m, f["ventaNeta"]);
        }

        [Fact]
        public async Task Ejecutar_SinMovimientosElCostoEsCeroYLaUtilidadEsLaVenta()
        {
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, venta: 80m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(0m, f["costo"]);
            Assert.Equal(80m, f["utilidad"]);
        }

        [Fact]
        public async Task Ejecutar_NotaDeDebitoApareceComoTal()
        {
            var r = Crear(new[] { Fac(1, subTipo: "ND") }, new[] { Lin(1, 1) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal("Nota de débito", f["tipo"]);
        }

        [Fact]
        public async Task Ejecutar_OrdenNaturalPorFechaDocumentoYLinea()
        {
            var r = Crear(
                new[] { Fac(2, fecha: Enero10), Fac(1, fecha: Enero10.AddDays(1)), Fac(3, fecha: Enero10) },
                new[] { Lin(1, 1, art: "D"), Lin(2, 2, art: "B"), Lin(2, 1, art: "A"), Lin(3, 1, art: "C") });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "A", "B", "C", "D" }, filas.Select(f => (string)f["articulo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_ExcluyeCancelados()
        {
            var r = Crear(new[] { Fac(1, cancelado: "S"), Fac(2) }, new[] { Lin(1, 1), Lin(2, 1) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(20, f["numDoc"]);
        }

        [Fact]
        public async Task Ejecutar_FiltraPorFechas()
        {
            var r = Crear(
                new[] { Fac(1, fecha: new DateTime(2026, 1, 5)), Fac(2, fecha: new DateTime(2026, 1, 31)), Fac(3, fecha: new DateTime(2026, 2, 1)) },
                new[] { Lin(1, 1), Lin(2, 1), Lin(3, 1) });

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10", Hasta = "2026-01-31" }), 100);

            Assert.Equal(20, Assert.Single(filas)["numDoc"]);
        }

        [Fact]
        public async Task Ejecutar_FiltraPorCliente()
        {
            var r = Crear(new[] { Fac(1, sn: "C001"), Fac(2, sn: "C002") }, new[] { Lin(1, 1), Lin(2, 1) });

            var filas = await r.EjecutarAsync(Filtros(d => d["cliente"] = new FiltroValor { Valor = "C002" }), 100);

            Assert.Equal("C002", Assert.Single(filas)["codigoSn"]);
        }

        [Fact]
        public async Task Ejecutar_FiltraPorArticulo()
        {
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, art: "A1"), Lin(1, 2, art: "A2") });

            var filas = await r.EjecutarAsync(Filtros(d => d["articulo"] = new FiltroValor { Valor = "A2" }), 100);

            Assert.Equal("A2", Assert.Single(filas)["articulo"]);
        }

        [Fact]
        public async Task Ejecutar_FiltraPorAlmacen()
        {
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, almacen: "01"), Lin(1, 2, almacen: "02") });

            var filas = await r.EjecutarAsync(Filtros(d => d["almacen"] = new FiltroValor { Valor = "02" }), 100);

            Assert.Equal("02", Assert.Single(filas)["almacen"]);
        }

        [Fact]
        public async Task Ejecutar_FiltraPorGrupo()
        {
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, art: "A1", grupo: "Lubricantes"), Lin(1, 2, art: "A2", grupo: "Frenos") });

            var filas = await r.EjecutarAsync(Filtros(d => d["grupo"] = new FiltroValor { Valor = "Fren" }), 100);

            Assert.Equal("A2", Assert.Single(filas)["articulo"]);
        }

        [Fact]
        public async Task Ejecutar_RespetaElLimite()
        {
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1), Lin(1, 2), Lin(1, 3) });

            var filas = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(2, filas.Count);
        }

        [Fact]
        public async Task Ejecutar_ConMasDeMaxLineasRespetaElLimiteSinLanzarYConSusCostos()
        {
            var lineas = new List<FacturaDetalle>(ConsultaLineasVenta.MaxLineas + 1);
            for (var i = 1; i <= ConsultaLineasVenta.MaxLineas + 1; i++)
                lineas.Add(Lin(1, i));
            var movimientos = Enumerable.Range(1, 10).Select(i => Mov(1, i, 2m, -7m * i)).ToList();
            var r = Crear(new[] { Fac(1) }, lineas, movimientos);

            var filas = await r.EjecutarAsync(Filtros(), 10);

            Assert.Equal(10, filas.Count);
            Assert.Equal(Enumerable.Range(1, 10).Select(i => 7m * i), filas.Select(f => (decimal)f["costo"]!));
        }
    }
}
