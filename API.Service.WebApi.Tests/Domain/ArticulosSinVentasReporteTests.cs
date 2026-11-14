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
    public class ArticulosSinVentasReporteTests
    {
        private static readonly DateTime Enero10 = new(2026, 1, 10);

        private readonly Mock<IRepositorioGenerico<Articulo, string>> _articulos = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _facturas = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _detalles = new();
        private readonly Mock<IRepositorioGenerico<MovimientoInventario, int>> _movimientos = new();

        private ArticulosSinVentasReporte Crear(IEnumerable<Articulo> articulos, IEnumerable<Factura>? facturas = null, IEnumerable<FacturaDetalle>? detalles = null)
        {
            _articulos.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(articulos.AsAsyncQueryable());
            _facturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((facturas ?? Array.Empty<Factura>()).AsAsyncQueryable());
            _detalles.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((detalles ?? Array.Empty<FacturaDetalle>()).AsAsyncQueryable());
            _movimientos.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(Array.Empty<MovimientoInventario>().AsAsyncQueryable());
            return new ArticulosSinVentasReporte(_articulos.Object,
                new ConsultaLineasVenta(_facturas.Object, _detalles.Object, _movimientos.Object));
        }

        private static Articulo Art(string codigo, string activo = "S", string venta = "S", string? grupo = "Lubricantes",
            decimal? disponible = 5m, decimal costoPromedio = 10m, decimal valorInventario = 50m) =>
            new()
            {
                Codigo = codigo, Nombre = "Artículo " + codigo, Activo = activo, ArticuloVenta = venta, CantDisponible = disponible,
                CostoPromedio = costoPromedio, ValorInventario = valorInventario,
                CodigoGrupoNavigation = grupo is null ? null : new GrupoArticulo { Codigo = 100, Nombre = grupo }
            };

        private static Factura Fac(int entry, DateTime? fecha = null, string cancelado = "N") =>
            new()
            {
                Entry = entry, NumDoc = entry * 10, Serie = 1, Cancelado = cancelado, DocSubType = "--",
                FechaDoc = fecha ?? Enero10, CodigoSn = "C001", NombreSn = "Cliente Uno", MonedaDoc = "GTQ"
            };

        private static FacturaDetalle Lin(int entry, int noLinea, string art) =>
            new()
            {
                Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = 1m, CantidadBase = 1m, TotalLinea = 112m, Impuesto = 12m,
                CodAlmacen = "01", PrctjeDesc = 0m, CodArticuloNavigation = new Articulo { Codigo = art, Nombre = "Artículo " + art }
            };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor> { ["fechas"] = new FiltroValor { Desde = "2026-01-01", Hasta = "2026-01-31" } };
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        [Fact]
        public void Definicion_EsDeVentasConFechasObligatorias()
        {
            var d = Crear(Array.Empty<Articulo>()).Definicion;

            Assert.Equal("articulos-sin-ventas", d.Codigo);
            Assert.Equal("Artículos sin ventas", d.Nombre);
            Assert.Equal("ReportesVentas", d.Modulo);
            Assert.Equal("Ventas", d.Familia);
            Assert.Equal(new[] { "fechas", "grupo" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.True(d.Filtros.Single(f => f.Campo == "fechas").Obligatorio);
            Assert.Equal(TipoFiltro.RangoFechas, d.Filtros.Single(f => f.Campo == "fechas").Tipo);
            Assert.Equal(TipoFiltro.Texto, d.Filtros.Single(f => f.Campo == "grupo").Tipo);
            Assert.Equal(new[] { "articulo", "nombre", "grupo", "existencias", "costoUnitario", "valorInventario" }, d.Columnas.Select(c => c.Campo).ToArray());
            Assert.True(d.Columnas.Single(c => c.Campo == "grupo").Agrupable);
            Assert.Equal("En stock", d.Columnas.Single(c => c.Campo == "existencias").Titulo);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "existencias").Totaliza);
            Assert.Equal("Costo promedio", d.Columnas.Single(c => c.Campo == "costoUnitario").Titulo);
            Assert.Equal(Totalizacion.Ninguna, d.Columnas.Single(c => c.Campo == "costoUnitario").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "valorInventario").Totaliza);
        }

        [Fact]
        public async Task Ejecutar_DevuelveLosDatosDelArticuloSinVentas()
        {
            var r = Crear(new[] { Art("A1", disponible: 4m, costoPromedio: 12.5m, valorInventario: 50m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal("A1", f["articulo"]);
            Assert.Equal("Artículo A1", f["nombre"]);
            Assert.Equal("Lubricantes", f["grupo"]);
            Assert.Equal(4m, f["existencias"]);
            Assert.Equal(12.5m, f["costoUnitario"]);
            Assert.Equal(50m, f["valorInventario"]);
        }

        [Fact]
        public async Task Ejecutar_SinExistenciasDefinidasMuestraCero()
        {
            var r = Crear(new[] { Art("A1", disponible: null) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(0m, f["existencias"]);
        }

        [Fact]
        public async Task Ejecutar_ExcluyeLosVendidosEnElPeriodo()
        {
            var r = Crear(
                new[] { Art("A1"), Art("A2") },
                new[] { Fac(1) },
                new[] { Lin(1, 1, "A1") });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal("A2", f["articulo"]);
        }

        [Fact]
        public async Task Ejecutar_UnaVentaFueraDelPeriodoOCanceladaNoCuenta()
        {
            var r = Crear(
                new[] { Art("A1"), Art("A2"), Art("A3") },
                new[] { Fac(1, fecha: new DateTime(2025, 12, 31)), Fac(2, cancelado: "S"), Fac(3, fecha: new DateTime(2026, 2, 1)) },
                new[] { Lin(1, 1, "A1"), Lin(2, 1, "A2"), Lin(3, 1, "A3") });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "A1", "A2", "A3" }, filas.Select(f => (string)f["articulo"]!).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public async Task Ejecutar_ExcluyeInactivosYNoVendibles()
        {
            var r = Crear(new[] { Art("A1"), Art("A2", activo: "N"), Art("A3", venta: "N") });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal("A1", f["articulo"]);
        }

        [Fact]
        public async Task Ejecutar_FiltraPorGrupo()
        {
            var r = Crear(new[] { Art("A1", grupo: "Lubricantes"), Art("A2", grupo: "Frenos"), Art("A3", grupo: null) });

            var filas = await r.EjecutarAsync(Filtros(d => d["grupo"] = new FiltroValor { Valor = "Fren" }), 100);

            Assert.Equal("A2", Assert.Single(filas)["articulo"]);
        }

        [Fact]
        public async Task Ejecutar_OrdenaPorValorDeInventarioDescendenteYLuegoPorCodigo()
        {
            var r = Crear(new[]
            {
                Art("B", valorInventario: 10m), Art("A", valorInventario: 10m), Art("C", valorInventario: 500m), Art("D", valorInventario: 0m)
            });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "C", "A", "B", "D" }, filas.Select(f => (string)f["articulo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_RespetaElLimite()
        {
            var r = Crear(new[] { Art("A", valorInventario: 30m), Art("B", valorInventario: 20m), Art("C", valorInventario: 10m) });

            var filas = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(new[] { "A", "B" }, filas.Select(f => (string)f["articulo"]!).ToArray());
        }
    }
}
