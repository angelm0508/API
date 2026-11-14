using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ComprasPorArticuloReporteTests
    {
        private static readonly DateTime Enero10 = new(2026, 1, 10);

        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _facturas = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>> _detalles = new();

        private ComprasPorArticuloReporte Crear(IEnumerable<FacturaCompra> facturas, IEnumerable<FacturaCompraDetalle> detalles)
        {
            _facturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            _detalles.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(detalles.AsAsyncQueryable());
            return new ComprasPorArticuloReporte(_facturas.Object, _detalles.Object);
        }

        private static FacturaCompra Fac(int entry, DateTime? fecha = null, string cancelado = "N", string subTipo = "--", string sn = "P001",
            string nombreSn = "Proveedor Uno", string moneda = "GTQ", int serie = 1, int numDoc = 0) =>
            new()
            {
                Entry = entry, NumDoc = numDoc == 0 ? entry * 10 : numDoc, Serie = serie, Cancelado = cancelado, DocSubType = subTipo,
                FechaDoc = fecha ?? Enero10, CodigoSn = sn, NombreSn = nombreSn, MonedaDoc = moneda
            };

        private static FacturaCompraDetalle Lin(int entry, int noLinea, string? art = "A1", decimal cantidad = 2m, decimal compra = 100m,
            string almacen = "01", string? grupo = "Lubricantes", decimal? cantidadBase = null, decimal desc = 0m) =>
            new()
            {
                Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cantidad, CantidadBase = cantidadBase ?? cantidad,
                TotalLinea = compra + 12m, Impuesto = 12m, CodAlmacen = almacen, PrctjeDesc = desc,
                CodArticuloNavigation = art is null ? null : new Articulo
                {
                    Codigo = art, Nombre = "Artículo " + art,
                    CodigoGrupoNavigation = grupo is null ? null : new GrupoArticulo { Codigo = 100, Nombre = grupo }
                }
            };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor>();
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        [Fact]
        public void Definicion_EsDeComprasConSusFiltrosYColumnas()
        {
            var d = Crear(Array.Empty<FacturaCompra>(), Array.Empty<FacturaCompraDetalle>()).Definicion;

            Assert.Equal("compras-por-articulo", d.Codigo);
            Assert.Equal("Compras por artículo", d.Nombre);
            Assert.Equal("Cada línea comprada con su cantidad y compra neta; agrupa por artículo o proveedor para ver totales.", d.Descripcion);
            Assert.Equal("ReportesCompras", d.Modulo);
            Assert.Equal("Compras", d.Familia);
            Assert.Equal(new[] { "fechas", "proveedor", "articulo", "grupo", "almacen" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal(TipoFiltro.RangoFechas, d.Filtros.Single(f => f.Campo == "fechas").Tipo);
            Assert.Equal(TipoFiltro.Socio, d.Filtros.Single(f => f.Campo == "proveedor").Tipo);
            Assert.Equal("P", d.Filtros.Single(f => f.Campo == "proveedor").SubTipo);
            Assert.Equal(TipoFiltro.Articulo, d.Filtros.Single(f => f.Campo == "articulo").Tipo);
            Assert.Equal(TipoFiltro.Texto, d.Filtros.Single(f => f.Campo == "grupo").Tipo);
            Assert.Equal(TipoFiltro.Almacen, d.Filtros.Single(f => f.Campo == "almacen").Tipo);
            Assert.Equal(
                new[] { "fecha", "tipo", "numDoc", "codigoSn", "nombreSn", "articulo", "nombreArticulo", "grupo", "almacen", "moneda", "cantidad", "precioNeto", "descuento", "compraNeta" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal("Proveedor", d.Columnas.Single(c => c.Campo == "nombreSn").Titulo);
            Assert.Equal(TipoColumna.Cantidad, d.Columnas.Single(c => c.Campo == "cantidad").Tipo);
            Assert.Equal("Cantidad (base)", d.Columnas.Single(c => c.Campo == "cantidad").Titulo);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "cantidad").Totaliza);
            Assert.Equal(TipoColumna.Dinero, d.Columnas.Single(c => c.Campo == "precioNeto").Tipo);
            Assert.Equal(Totalizacion.Ninguna, d.Columnas.Single(c => c.Campo == "precioNeto").Totaliza);
            Assert.Equal(TipoColumna.Cantidad, d.Columnas.Single(c => c.Campo == "descuento").Tipo);
            Assert.Equal("Descuento %", d.Columnas.Single(c => c.Campo == "descuento").Titulo);
            Assert.Equal(Totalizacion.Ninguna, d.Columnas.Single(c => c.Campo == "descuento").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "compraNeta").Totaliza);
            Assert.True(d.Columnas.Single(c => c.Campo == "fecha").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "tipo").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "nombreSn").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "articulo").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "grupo").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "almacen").Agrupable);
            Assert.False(d.Columnas.Single(c => c.Campo == "codigoSn").VisiblePorDefecto);
            Assert.False(d.Columnas.Single(c => c.Campo == "moneda").VisiblePorDefecto);
        }

        [Fact]
        public async Task Ejecutar_UnaFilaPorLineaConCompraNetaYPrecioNeto()
        {
            var r = Crear(new[] { Fac(1, numDoc: 501) }, new[] { Lin(1, 1, cantidad: 4m, compra: 100m, desc: 5m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(Enero10, f["fecha"]);
            Assert.Equal("Factura de compra", f["tipo"]);
            Assert.Equal(501, f["numDoc"]);
            Assert.Equal("P001", f["codigoSn"]);
            Assert.Equal("Proveedor Uno", f["nombreSn"]);
            Assert.Equal("A1", f["articulo"]);
            Assert.Equal("Artículo A1", f["nombreArticulo"]);
            Assert.Equal("Lubricantes", f["grupo"]);
            Assert.Equal("01", f["almacen"]);
            Assert.Equal("GTQ", f["moneda"]);
            Assert.Equal(4m, f["cantidad"]);
            Assert.Equal(25m, f["precioNeto"]);
            Assert.Equal(5m, f["descuento"]);
            Assert.Equal(100m, f["compraNeta"]);
        }

        [Fact]
        public async Task Ejecutar_LaCompraNetaNoIncluyeElImpuesto()
        {
            var linea = Lin(1, 1, compra: 80m);
            linea.Impuesto = 30m;
            linea.TotalLinea = 110m;
            var r = Crear(new[] { Fac(1) }, new[] { linea });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(80m, f["compraNeta"]);
        }

        [Fact]
        public async Task Ejecutar_UsaLaCantidadBaseYElPrecioNetoEsPorUnidadBase()
        {
            // 2 cajas de 12 unidades = 24 unidades base; compra neta 120 => 5 por unidad base.
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, cantidad: 2m, cantidadBase: 24m, compra: 120m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(24m, f["cantidad"]);
            Assert.Equal(5m, f["precioNeto"]);
        }

        [Fact]
        public async Task Ejecutar_SinCantidadBaseUsaLaCantidad()
        {
            var linea = Lin(1, 1, cantidad: 4m, compra: 100m);
            linea.CantidadBase = null;
            var r = Crear(new[] { Fac(1) }, new[] { linea });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(4m, f["cantidad"]);
            Assert.Equal(25m, f["precioNeto"]);
        }

        [Fact]
        public async Task Ejecutar_SinCantidadElPrecioNetoEsCero()
        {
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, cantidad: 0m, compra: 50m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(0m, f["precioNeto"]);
            Assert.Equal(50m, f["compraNeta"]);
        }

        [Fact]
        public async Task Ejecutar_ImportesNulosCuentanComoCero()
        {
            var linea = Lin(1, 1);
            linea.TotalLinea = null; linea.Impuesto = null; linea.PrctjeDesc = null;
            var r = Crear(new[] { Fac(1) }, new[] { linea });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(0m, f["compraNeta"]);
            Assert.Equal(0m, f["descuento"]);
        }

        [Fact]
        public async Task Ejecutar_NotaDeDebitoDeCompraApareceComoTal()
        {
            var r = Crear(new[] { Fac(1, subTipo: "DN") }, new[] { Lin(1, 1) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal("Nota de débito de compra", f["tipo"]);
        }

        [Fact]
        public async Task Ejecutar_ExcluyeCanceladosYLineasSinArticulo()
        {
            var r = Crear(
                new[] { Fac(1, cancelado: "S"), Fac(2) },
                new[] { Lin(1, 1), Lin(2, 1), Lin(2, 2, art: null) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(20, f["numDoc"]);
            Assert.Equal("A1", f["articulo"]);
        }

        [Fact]
        public async Task Ejecutar_ExcluyeDocumentosQueNoSonFacturaNiNotaDeDebito()
        {
            var r = Crear(new[] { Fac(1, subTipo: "XX"), Fac(2) }, new[] { Lin(1, 1), Lin(2, 1) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(20, f["numDoc"]);
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
        public async Task Ejecutar_FiltraPorFechas()
        {
            var r = Crear(
                new[] { Fac(1, fecha: new DateTime(2026, 1, 5)), Fac(2, fecha: new DateTime(2026, 1, 31, 15, 0, 0)), Fac(3, fecha: new DateTime(2026, 2, 1)) },
                new[] { Lin(1, 1), Lin(2, 1), Lin(3, 1) });

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10", Hasta = "2026-01-31" }), 100);

            Assert.Equal(20, Assert.Single(filas)["numDoc"]);
        }

        [Fact]
        public async Task Ejecutar_FiltraPorProveedor()
        {
            var r = Crear(new[] { Fac(1, sn: "P001"), Fac(2, sn: "P002") }, new[] { Lin(1, 1), Lin(2, 1) });

            var filas = await r.EjecutarAsync(Filtros(d => d["proveedor"] = new FiltroValor { Valor = "P002" }), 100);

            Assert.Equal("P002", Assert.Single(filas)["codigoSn"]);
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
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, art: "A1", grupo: "Lubricantes"), Lin(1, 2, art: "A2", grupo: "Frenos"), Lin(1, 3, art: "A3", grupo: null) });

            var filas = await r.EjecutarAsync(Filtros(d => d["grupo"] = new FiltroValor { Valor = "Fren" }), 100);

            Assert.Equal("A2", Assert.Single(filas)["articulo"]);
        }

        [Fact]
        public async Task Ejecutar_SinGrupoElGrupoVaVacio()
        {
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, grupo: null) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Null(f["grupo"]);
        }

        [Fact]
        public async Task Ejecutar_RespetaElLimite()
        {
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 3, art: "C"), Lin(1, 1, art: "A"), Lin(1, 2, art: "B") });

            var filas = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(new[] { "A", "B" }, filas.Select(f => (string)f["articulo"]!).ToArray());
        }
    }
}
