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
    public class ArticulosMasVendidosReporteTests
    {
        private static readonly DateTime Enero10 = new(2026, 1, 10);

        private readonly Mock<IRepositorioGenerico<Factura, int>> _facturas = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _detalles = new();
        private readonly Mock<IRepositorioGenerico<MovimientoInventario, int>> _movimientos = new();

        private ArticulosMasVendidosReporte Crear(IEnumerable<Factura> facturas, IEnumerable<FacturaDetalle> detalles, IEnumerable<MovimientoInventario>? movimientos = null)
        {
            _facturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            _detalles.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(detalles.AsAsyncQueryable());
            _movimientos.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((movimientos ?? Array.Empty<MovimientoInventario>()).AsAsyncQueryable());
            return new ArticulosMasVendidosReporte(new ConsultaLineasVenta(_facturas.Object, _detalles.Object, _movimientos.Object));
        }

        private static Factura Fac(int entry, DateTime? fecha = null, string cancelado = "N", string sn = "C001", string moneda = "GTQ") =>
            new()
            {
                Entry = entry, NumDoc = entry * 10, Serie = 1, Cancelado = cancelado, DocSubType = "--",
                FechaDoc = fecha ?? Enero10, CodigoSn = sn, NombreSn = "Cliente " + sn, MonedaDoc = moneda
            };

        private static FacturaDetalle Lin(int entry, int noLinea, string art, decimal cantidad, decimal venta,
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

        private static MovimientoInventario Mov(int docEntry, int docLinea, decimal sale, decimal costoTotal) =>
            new() { TipoDoc = "6", DocEntry = docEntry, DocLinea = docLinea, CodArticulo = "A1", CodAlmacen = "01", CantidadSale = sale, ValorMovimiento = -costoTotal };

        private static readonly FiltroValor Enero = new() { Desde = "2026-01-01", Hasta = "2026-01-31" };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor> { ["fechas"] = Enero };
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        // A1: más cantidad (10), poca venta y utilidad. A2: más venta, poca cantidad. A3: más utilidad.
        // Cantidad: A1, A3, A2 · Venta: A2, A3, A1 · Utilidad: A3, A2, A1.
        private ArticulosMasVendidosReporte CrearConTresArticulos() => Crear(
            new[] { Fac(1) },
            new[] { Lin(1, 1, "A1", 10m, 100m), Lin(1, 2, "A2", 2m, 500m), Lin(1, 3, "A3", 5m, 300m) },
            new[] { Mov(1, 1, 10m, 90m), Mov(1, 2, 2m, 400m), Mov(1, 3, 5m, 100m) });

        private static string[] Articulos(IReadOnlyList<FilaReporte> filas) => filas.Select(f => (string)f["articulo"]!).ToArray();

        [Fact]
        public void Definicion_EsDeVentasConFechasObligatoriasYMedirPorConValorPorDefecto()
        {
            var d = Crear(Array.Empty<Factura>(), Array.Empty<FacturaDetalle>()).Definicion;

            Assert.Equal("articulos-mas-vendidos", d.Codigo);
            Assert.Equal("Artículos más vendidos", d.Nombre);
            Assert.Equal("ReportesVentas", d.Modulo);
            Assert.Equal("Ventas", d.Familia);
            Assert.Equal(new[] { "fechas", "cliente", "grupo", "almacen", "medirPor", "primeros" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.True(d.Filtros.Single(f => f.Campo == "fechas").Obligatorio);
            Assert.Equal("C", d.Filtros.Single(f => f.Campo == "cliente").SubTipo);
            var medir = d.Filtros.Single(f => f.Campo == "medirPor");
            Assert.Equal(TipoFiltro.Lista, medir.Tipo);
            Assert.False(medir.Obligatorio);
            Assert.Equal("cantidad", medir.ValorPorDefecto);
            Assert.Equal(new[] { "cantidad", "venta", "utilidad" }, medir.Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(TipoFiltro.Numero, d.Filtros.Single(f => f.Campo == "primeros").Tipo);
            Assert.Equal(
                new[] { "posicion", "articulo", "nombreArticulo", "grupo", "moneda", "cantidad", "ventaNeta", "costo", "utilidad", "documentos" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.False(d.Columnas.Single(c => c.Campo == "moneda").VisiblePorDefecto);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "cantidad").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "ventaNeta").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "costo").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "utilidad").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "documentos").Totaliza);
            Assert.Equal("Facturas", d.Columnas.Single(c => c.Campo == "documentos").Titulo);
            Assert.True(d.Columnas.Single(c => c.Campo == "grupo").Agrupable);
        }

        [Fact]
        public async Task Ejecutar_PorDefectoOrdenaPorCantidad()
        {
            var filas = await CrearConTresArticulos().EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "A1", "A3", "A2" }, Articulos(filas));
            Assert.Equal(new[] { 1, 2, 3 }, filas.Select(f => (int)f["posicion"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_MedirPorCantidad()
        {
            var filas = await CrearConTresArticulos().EjecutarAsync(Filtros(d => d["medirPor"] = new FiltroValor { Valor = "cantidad" }), 100);

            Assert.Equal(new[] { "A1", "A3", "A2" }, Articulos(filas));
        }

        [Fact]
        public async Task Ejecutar_MedirPorVenta()
        {
            var filas = await CrearConTresArticulos().EjecutarAsync(Filtros(d => d["medirPor"] = new FiltroValor { Valor = "venta" }), 100);

            Assert.Equal(new[] { "A2", "A3", "A1" }, Articulos(filas));
        }

        [Fact]
        public async Task Ejecutar_MedirPorUtilidad()
        {
            var filas = await CrearConTresArticulos().EjecutarAsync(Filtros(d => d["medirPor"] = new FiltroValor { Valor = "utilidad" }), 100);

            Assert.Equal(new[] { "A3", "A2", "A1" }, Articulos(filas));
            Assert.Equal(new[] { 200m, 100m, 10m }, filas.Select(f => (decimal)f["utilidad"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_EmpateSeDesempataPorCodigoDeArticulo()
        {
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, "B", 3m, 30m), Lin(1, 2, "A", 3m, 30m), Lin(1, 3, "C", 3m, 30m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "A", "B", "C" }, Articulos(filas));
        }

        [Fact]
        public async Task Ejecutar_PrimerosRecortaYLaPosicionSeAsignaDespues()
        {
            var filas = await CrearConTresArticulos().EjecutarAsync(
                Filtros(d => { d["medirPor"] = new FiltroValor { Valor = "venta" }; d["primeros"] = new FiltroValor { Valor = "2" }; }), 100);

            Assert.Equal(new[] { "A2", "A3" }, Articulos(filas));
            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["posicion"]!).ToArray());
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("2.5")]
        public async Task Ejecutar_PrimerosInvalidoLanzaExcepcion(string primeros)
        {
            var r = CrearConTresArticulos();

            var ex = await Assert.ThrowsAsync<ReporteInvalidoException>(
                () => r.EjecutarAsync(Filtros(d => d["primeros"] = new FiltroValor { Valor = primeros }), 100));

            Assert.Equal("El filtro 'Mostrar los primeros' debe ser un entero mayor que cero.", ex.Message);
        }

        [Fact]
        public async Task Ejecutar_AgregaLasLineasDelMismoArticuloEnVariasFacturas()
        {
            var r = Crear(
                new[] { Fac(1), Fac(2), Fac(3) },
                new[] { Lin(1, 1, "A1", 2m, 100m), Lin(2, 1, "A1", 3m, 150m), Lin(3, 1, "A1", 5m, 250m) },
                new[] { Mov(1, 1, 2m, 40m), Mov(2, 1, 3m, 60m), Mov(3, 1, 5m, 100m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal("A1", f["articulo"]);
            Assert.Equal("Artículo A1", f["nombreArticulo"]);
            Assert.Equal("Lubricantes", f["grupo"]);
            Assert.Equal("GTQ", f["moneda"]);
            Assert.Equal(10m, f["cantidad"]);
            Assert.Equal(500m, f["ventaNeta"]);
            Assert.Equal(200m, f["costo"]);
            Assert.Equal(300m, f["utilidad"]);
            Assert.Equal(3, f["documentos"]);
        }

        [Fact]
        public async Task Ejecutar_DosMonedasDelMismoArticuloSonFilasSeparadas()
        {
            var r = Crear(
                new[] { Fac(1, moneda: "GTQ"), Fac(2, moneda: "USD") },
                new[] { Lin(1, 1, "A1", 2m, 100m), Lin(2, 1, "A1", 1m, 20m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(2, filas.Count);
            var gtq = filas.Single(f => (string?)f["moneda"] == "GTQ");
            var usd = filas.Single(f => (string?)f["moneda"] == "USD");
            Assert.Equal(100m, gtq["ventaNeta"]);
            Assert.Equal(20m, usd["ventaNeta"]);
        }

        [Fact]
        public async Task Ejecutar_DocumentosCuentaFacturasDistintasNoLineas()
        {
            var r = Crear(
                new[] { Fac(1), Fac(2) },
                new[] { Lin(1, 1, "A1", 1m, 10m), Lin(1, 2, "A1", 1m, 10m), Lin(2, 1, "A1", 1m, 10m), Lin(2, 2, "A2", 1m, 10m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(2, filas.Single(f => (string)f["articulo"]! == "A1")["documentos"]);
            Assert.Equal(1, filas.Single(f => (string)f["articulo"]! == "A2")["documentos"]);
        }

        [Fact]
        public async Task Ejecutar_ExcluyeCancelados()
        {
            var r = Crear(new[] { Fac(1, cancelado: "S"), Fac(2) }, new[] { Lin(1, 1, "A1", 5m, 500m), Lin(2, 1, "A2", 1m, 10m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal("A2", f["articulo"]);
        }

        [Fact]
        public async Task Ejecutar_FiltraPorFechas()
        {
            var r = Crear(
                new[] { Fac(1, fecha: new DateTime(2025, 12, 31)), Fac(2, fecha: new DateTime(2026, 1, 31)), Fac(3, fecha: new DateTime(2026, 2, 1)) },
                new[] { Lin(1, 1, "A1", 1m, 10m), Lin(2, 1, "A2", 1m, 10m), Lin(3, 1, "A3", 1m, 10m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal("A2", Assert.Single(filas)["articulo"]);
        }

        [Fact]
        public async Task Ejecutar_FiltraPorClienteGrupoYAlmacen()
        {
            var r = Crear(
                new[] { Fac(1, sn: "C001"), Fac(2, sn: "C002") },
                new[]
                {
                    Lin(1, 1, "A1", 1m, 10m, almacen: "01", grupo: "Lubricantes"),
                    Lin(2, 1, "A2", 1m, 10m, almacen: "01", grupo: "Lubricantes"),
                    Lin(2, 2, "A3", 1m, 10m, almacen: "02", grupo: "Lubricantes"),
                    Lin(2, 3, "A4", 1m, 10m, almacen: "01", grupo: "Frenos")
                });

            Assert.Equal("A2", Assert.Single(await r.EjecutarAsync(Filtros(d =>
            {
                d["cliente"] = new FiltroValor { Valor = "C002" };
                d["grupo"] = new FiltroValor { Valor = "Lubric" };
                d["almacen"] = new FiltroValor { Valor = "01" };
            }), 100))["articulo"]);
        }

        [Fact]
        public async Task Ejecutar_RespetaElLimite()
        {
            var filas = await CrearConTresArticulos().EjecutarAsync(Filtros(), 2);

            Assert.Equal(new[] { "A1", "A3" }, Articulos(filas));
        }
    }
}
