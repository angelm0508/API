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
    public class VentasPorClienteReporteTests
    {
        private static readonly DateTime Enero10 = new(2026, 1, 10);

        private readonly Mock<IRepositorioGenerico<Factura, int>> _facturas = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _detalles = new();
        private readonly Mock<IRepositorioGenerico<MovimientoInventario, int>> _movimientos = new();
        private readonly Mock<IRepositorioGenerico<NotaCredito, int>> _notas = new();

        private VentasPorClienteReporte Crear(
            IEnumerable<Factura> facturas, IEnumerable<FacturaDetalle> detalles,
            IEnumerable<MovimientoInventario>? movimientos = null, IEnumerable<NotaCredito>? notas = null)
        {
            _facturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            _detalles.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(detalles.AsAsyncQueryable());
            _movimientos.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((movimientos ?? Array.Empty<MovimientoInventario>()).AsAsyncQueryable());
            _notas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((notas ?? Array.Empty<NotaCredito>()).AsAsyncQueryable());
            return new VentasPorClienteReporte(new ConsultaLineasVenta(_facturas.Object, _detalles.Object, _movimientos.Object), _notas.Object);
        }

        private static Factura Fac(int entry, string sn, DateTime? fecha = null, string cancelado = "N", string moneda = "GTQ") =>
            new()
            {
                Entry = entry, NumDoc = entry * 10, Serie = 1, Cancelado = cancelado, DocSubType = "--",
                FechaDoc = fecha ?? Enero10, CodigoSn = sn, NombreSn = "Cliente " + sn, MonedaDoc = moneda
            };

        private static FacturaDetalle Lin(int entry, int noLinea, decimal venta, string art = "A1") =>
            new()
            {
                Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = 1m, CantidadBase = 1m,
                TotalLinea = venta + 12m, Impuesto = 12m, CodAlmacen = "01", PrctjeDesc = 0m,
                CodArticuloNavigation = new Articulo { Codigo = art, Nombre = "Artículo " + art }
            };

        private static MovimientoInventario Mov(int docEntry, int docLinea, decimal costoTotal) =>
            new() { TipoDoc = "6", DocEntry = docEntry, DocLinea = docLinea, CodArticulo = "A1", CodAlmacen = "01", CantidadSale = 1m, ValorMovimiento = -costoTotal };

        private static NotaCredito Nc(int entry, string sn, decimal neto, string cancelado = "N", string moneda = "GTQ", DateTime? fecha = null) =>
            new()
            {
                Entry = entry, NumDoc = entry, Serie = 2, Cancelado = cancelado, FechaDoc = fecha ?? Enero10,
                CodigoSn = sn, NombreSn = "Cliente " + sn, MonedaDoc = moneda, TotalImp = 12m, TotalDoc = neto + 12m
            };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor> { ["fechas"] = new FiltroValor { Desde = "2026-01-01", Hasta = "2026-01-31" } };
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        private static string[] Clientes(IReadOnlyList<FilaReporte> filas) => filas.Select(f => (string)f["codigoSn"]!).ToArray();

        // C1: venta 1,000 / costo 900 → utilidad 100. C2: venta 600 / costo 200 → 400. C3: venta 300 / costo 100 → 200.
        // Venta: C1, C2, C3 · Utilidad: C2, C3, C1.
        private VentasPorClienteReporte CrearConTresClientes() => Crear(
            new[] { Fac(1, "C1"), Fac(2, "C2"), Fac(3, "C3") },
            new[] { Lin(1, 1, 1000m), Lin(2, 1, 600m), Lin(3, 1, 300m) },
            new[] { Mov(1, 1, 900m), Mov(2, 1, 200m), Mov(3, 1, 100m) });

        [Fact]
        public void Definicion_EsDeVentasConFechasObligatoriasYMedirPorConValorPorDefecto()
        {
            var d = Crear(Array.Empty<Factura>(), Array.Empty<FacturaDetalle>()).Definicion;

            Assert.Equal("ventas-por-cliente", d.Codigo);
            Assert.Equal("Ventas por cliente", d.Nombre);
            Assert.Equal("Venta neta, costo y utilidad por cliente; las notas de crédito restan.", d.Descripcion);
            Assert.Equal("ReportesVentas", d.Modulo);
            Assert.Equal("Ventas", d.Familia);
            Assert.Equal(new[] { "fechas", "cliente", "medirPor", "primeros" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.True(d.Filtros.Single(f => f.Campo == "fechas").Obligatorio);
            Assert.Equal("C", d.Filtros.Single(f => f.Campo == "cliente").SubTipo);
            var medir = d.Filtros.Single(f => f.Campo == "medirPor");
            Assert.Equal(TipoFiltro.Lista, medir.Tipo);
            Assert.Equal("venta", medir.ValorPorDefecto);
            Assert.Equal(new[] { "venta", "utilidad" }, medir.Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(new[] { "Venta neta total", "Utilidad" }, medir.Opciones!.Select(o => o.Titulo).ToArray());
            Assert.Equal(TipoFiltro.Numero, d.Filtros.Single(f => f.Campo == "primeros").Tipo);
            Assert.Equal(
                new[] { "posicion", "codigoSn", "nombreSn", "moneda", "ventas", "notasCredito", "ventaNeta", "costo", "utilidad", "documentos" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal("#", d.Columnas.Single(c => c.Campo == "posicion").Titulo);
            Assert.Equal("Código", d.Columnas.Single(c => c.Campo == "codigoSn").Titulo);
            Assert.Equal("Cliente", d.Columnas.Single(c => c.Campo == "nombreSn").Titulo);
            Assert.False(d.Columnas.Single(c => c.Campo == "moneda").VisiblePorDefecto);
            foreach (var c in new[] { "ventas", "notasCredito", "ventaNeta", "costo", "utilidad" })
            {
                Assert.Equal(TipoColumna.Dinero, d.Columnas.Single(x => x.Campo == c).Tipo);
                Assert.Equal(Totalizacion.Suma, d.Columnas.Single(x => x.Campo == c).Totaliza);
            }
            Assert.Equal("Venta neta total", d.Columnas.Single(c => c.Campo == "ventaNeta").Titulo);
            Assert.Equal(TipoColumna.Entero, d.Columnas.Single(c => c.Campo == "documentos").Tipo);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "documentos").Totaliza);
            Assert.Equal("Facturas", d.Columnas.Single(c => c.Campo == "documentos").Titulo);
        }

        [Fact]
        public async Task Ejecutar_PorDefectoOrdenaPorVentaNetaTotal()
        {
            var filas = await CrearConTresClientes().EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "C1", "C2", "C3" }, Clientes(filas));
            Assert.Equal(new[] { 1, 2, 3 }, filas.Select(f => (int)f["posicion"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_MedirPorUtilidadCambiaElOrden()
        {
            var filas = await CrearConTresClientes().EjecutarAsync(Filtros(d => d["medirPor"] = new FiltroValor { Valor = "utilidad" }), 100);

            Assert.Equal(new[] { "C2", "C3", "C1" }, Clientes(filas));
            Assert.Equal(new[] { 400m, 200m, 100m }, filas.Select(f => (decimal)f["utilidad"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_LaNotaDeCreditoRestaYPuedeCambiarElRanking()
        {
            // C1 vende 1,000 pero devuelve 800 → venta neta 200 y baja al último lugar.
            var r = Crear(
                new[] { Fac(1, "C1"), Fac(2, "C2"), Fac(3, "C3") },
                new[] { Lin(1, 1, 1000m), Lin(2, 1, 600m), Lin(3, 1, 300m) },
                new[] { Mov(1, 1, 900m), Mov(2, 1, 200m), Mov(3, 1, 100m) },
                new[] { Nc(1, "C1", 800m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "C2", "C3", "C1" }, Clientes(filas));
            var c1 = filas.Single(f => (string)f["codigoSn"]! == "C1");
            Assert.Equal(1000m, c1["ventas"]);
            Assert.Equal(800m, c1["notasCredito"]);
            Assert.Equal(200m, c1["ventaNeta"]);
            Assert.Equal(900m, c1["costo"]);
            Assert.Equal(-700m, c1["utilidad"]);
            Assert.Equal(1, c1["documentos"]);
            Assert.Equal("Cliente C1", c1["nombreSn"]);
            Assert.Equal("GTQ", c1["moneda"]);
        }

        [Fact]
        public async Task Ejecutar_ClienteConSoloNotasDeCreditoApareceConVentasCero()
        {
            var r = Crear(
                new[] { Fac(1, "C1") }, new[] { Lin(1, 1, 100m) },
                notas: new[] { Nc(1, "C9", 50m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "C1", "C9" }, Clientes(filas));
            var c9 = filas[1];
            Assert.Equal("Cliente C9", c9["nombreSn"]);
            Assert.Equal(0m, c9["ventas"]);
            Assert.Equal(50m, c9["notasCredito"]);
            Assert.Equal(-50m, c9["ventaNeta"]);
            Assert.Equal(-50m, c9["utilidad"]);
            Assert.Equal(0, c9["documentos"]);
        }

        [Fact]
        public async Task Ejecutar_NotaDeCreditoCanceladaNoResta()
        {
            var r = Crear(
                new[] { Fac(1, "C1") }, new[] { Lin(1, 1, 100m) },
                notas: new[] { Nc(1, "C1", 80m, cancelado: "S"), Nc(2, "C7", 10m, cancelado: "S") });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal("C1", f["codigoSn"]);
            Assert.Equal(0m, f["notasCredito"]);
            Assert.Equal(100m, f["ventaNeta"]);
        }

        [Fact]
        public async Task Ejecutar_FacturaCanceladaNoCuenta()
        {
            var r = Crear(new[] { Fac(1, "C1", cancelado: "S"), Fac(2, "C2") }, new[] { Lin(1, 1, 900m), Lin(2, 1, 100m) });

            Assert.Equal("C2", Assert.Single(await r.EjecutarAsync(Filtros(), 100))["codigoSn"]);
        }

        [Fact]
        public async Task Ejecutar_EmpateSeDesempataPorCodigoDeCliente()
        {
            var r = Crear(
                new[] { Fac(1, "C3"), Fac(2, "C1"), Fac(3, "C2") },
                new[] { Lin(1, 1, 50m), Lin(2, 1, 50m), Lin(3, 1, 50m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "C1", "C2", "C3" }, Clientes(filas));
        }

        [Fact]
        public async Task Ejecutar_DocumentosCuentaFacturasDistintas()
        {
            var r = Crear(
                new[] { Fac(1, "C1"), Fac(2, "C1") },
                new[] { Lin(1, 1, 10m), Lin(1, 2, 10m), Lin(2, 1, 10m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(2, f["documentos"]);
            Assert.Equal(30m, f["ventas"]);
        }

        [Fact]
        public async Task Ejecutar_DosMonedasDelMismoClienteSonFilasSeparadas()
        {
            var r = Crear(
                new[] { Fac(1, "C1", moneda: "GTQ"), Fac(2, "C1", moneda: "USD") },
                new[] { Lin(1, 1, 100m), Lin(2, 1, 20m) },
                notas: new[] { Nc(1, "C1", 5m, moneda: "USD") });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(2, filas.Count);
            Assert.Equal(100m, filas.Single(f => (string?)f["moneda"] == "GTQ")["ventaNeta"]);
            var usd = filas.Single(f => (string?)f["moneda"] == "USD");
            Assert.Equal(15m, usd["ventaNeta"]);
            Assert.Equal(5m, usd["notasCredito"]);
        }

        [Fact]
        public async Task Ejecutar_FiltroDeClienteAplicaAVentasYNotas()
        {
            var r = Crear(
                new[] { Fac(1, "C1"), Fac(2, "C2") }, new[] { Lin(1, 1, 100m), Lin(2, 1, 200m) },
                notas: new[] { Nc(1, "C1", 10m), Nc(2, "C2", 20m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(d => d["cliente"] = new FiltroValor { Valor = "C2" }), 100));

            Assert.Equal("C2", f["codigoSn"]);
            Assert.Equal(180m, f["ventaNeta"]);
        }

        [Fact]
        public async Task Ejecutar_LasFechasLimitanVentasYNotas()
        {
            var r = Crear(
                new[] { Fac(1, "C1", new DateTime(2025, 12, 31)), Fac(2, "C2", new DateTime(2026, 1, 31)), Fac(3, "C3", new DateTime(2026, 2, 1)) },
                new[] { Lin(1, 1, 10m), Lin(2, 1, 10m), Lin(3, 1, 10m) },
                notas: new[] { Nc(1, "C4", 5m, fecha: new DateTime(2026, 2, 1)), Nc(2, "C5", 5m, fecha: new DateTime(2026, 1, 31)) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "C2", "C5" }, Clientes(filas).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public async Task Ejecutar_PrimerosRecortaYLaPosicionSeAsignaDespues()
        {
            var filas = await CrearConTresClientes().EjecutarAsync(
                Filtros(d => { d["medirPor"] = new FiltroValor { Valor = "utilidad" }; d["primeros"] = new FiltroValor { Valor = "2" }; }), 100);

            Assert.Equal(new[] { "C2", "C3" }, Clientes(filas));
            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["posicion"]!).ToArray());
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("2.5")]
        public async Task Ejecutar_PrimerosInvalidoLanzaExcepcion(string primeros)
        {
            var r = CrearConTresClientes();

            var ex = await Assert.ThrowsAsync<ReporteInvalidoException>(
                () => r.EjecutarAsync(Filtros(d => d["primeros"] = new FiltroValor { Valor = primeros }), 100));

            Assert.Equal("El filtro 'Mostrar los primeros' debe ser un entero mayor que cero.", ex.Message);
        }

        [Fact]
        public async Task Ejecutar_PrimerosMayorQueElLimiteSeRecortaPorElLimiteYLaPosicionSigueConsecutiva()
        {
            var filas = await CrearConTresClientes().EjecutarAsync(
                Filtros(d => d["primeros"] = new FiltroValor { Valor = "3" }), 2);

            Assert.Equal(new[] { "C1", "C2" }, Clientes(filas));
            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["posicion"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_RespetaElLimite()
        {
            var filas = await CrearConTresClientes().EjecutarAsync(Filtros(), 2);

            Assert.Equal(new[] { "C1", "C2" }, Clientes(filas));
        }
    }
}
