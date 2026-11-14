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
    public class UtilidadPorPeriodoReporteTests
    {
        private static readonly DateTime Enero10 = new(2026, 1, 10);
        private static readonly DateTime Febrero15 = new(2026, 2, 15);

        private readonly Mock<IRepositorioGenerico<Factura, int>> _facturas = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _detalles = new();
        private readonly Mock<IRepositorioGenerico<MovimientoInventario, int>> _movimientos = new();
        private readonly Mock<IRepositorioGenerico<NotaCredito, int>> _notas = new();

        private UtilidadPorPeriodoReporte Crear(
            IEnumerable<Factura> facturas, IEnumerable<FacturaDetalle> detalles,
            IEnumerable<MovimientoInventario>? movimientos = null, IEnumerable<NotaCredito>? notas = null)
        {
            _facturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            _detalles.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(detalles.AsAsyncQueryable());
            _movimientos.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((movimientos ?? Array.Empty<MovimientoInventario>()).AsAsyncQueryable());
            _notas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((notas ?? Array.Empty<NotaCredito>()).AsAsyncQueryable());
            return new UtilidadPorPeriodoReporte(new ConsultaLineasVenta(_facturas.Object, _detalles.Object, _movimientos.Object), _notas.Object, _facturas.Object);
        }

        private static Factura Fac(int entry, DateTime? fecha = null, string cancelado = "N", string sn = "C001", string moneda = "GTQ") =>
            new()
            {
                Entry = entry, NumDoc = entry * 10, Serie = 1, Cancelado = cancelado, DocSubType = "--",
                FechaDoc = fecha ?? Enero10, CodigoSn = sn, NombreSn = "Cliente " + sn, MonedaDoc = moneda
            };

        private static FacturaDetalle Lin(int entry, int noLinea, decimal venta, decimal impuesto = 12m, string art = "A1") =>
            new()
            {
                Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = 1m, CantidadBase = 1m,
                TotalLinea = venta + impuesto, Impuesto = impuesto, CodAlmacen = "01", PrctjeDesc = 0m,
                CodArticuloNavigation = new Articulo { Codigo = art, Nombre = "Artículo " + art }
            };

        // Nota de débito por concepto: vive en Factura (DocSubType "ND", DocType "S") y no tiene líneas con artículo.
        private static Factura NdConcepto(int entry, DateTime fecha, decimal neto, decimal iva = 12m, string cancelado = "N", string sn = "C001", string moneda = "GTQ") =>
            new()
            {
                Entry = entry, NumDoc = entry * 10, Serie = 3, Cancelado = cancelado, DocSubType = "ND", DocType = "S",
                FechaDoc = fecha, CodigoSn = sn, NombreSn = "Cliente " + sn, MonedaDoc = moneda, TotalImp = iva, TotalDoc = neto + iva
            };

        private static MovimientoInventario Mov(int docEntry, int docLinea, decimal costoTotal) =>
            new() { TipoDoc = "6", DocEntry = docEntry, DocLinea = docLinea, CodArticulo = "A1", CodAlmacen = "01", CantidadSale = 1m, ValorMovimiento = -costoTotal };

        private static NotaCredito Nc(int entry, DateTime fecha, decimal neto, decimal iva = 12m, string cancelado = "N", string sn = "C001", string moneda = "GTQ") =>
            new()
            {
                Entry = entry, NumDoc = entry, Serie = 2, Cancelado = cancelado, FechaDoc = fecha,
                CodigoSn = sn, NombreSn = "Cliente " + sn, MonedaDoc = moneda, TotalImp = iva, TotalDoc = neto + iva
            };

        private static FiltrosReporte Filtros(string desde = "2026-01-01", string hasta = "2026-12-31", Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor> { ["fechas"] = new FiltroValor { Desde = desde, Hasta = hasta } };
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        private static string[] Periodos(IReadOnlyList<FilaReporte> filas) => filas.Select(f => (string)f["periodo"]!).ToArray();

        [Fact]
        public void Definicion_EsDeVentasConFechasObligatoriasYPeriodoConValorPorDefecto()
        {
            var d = Crear(Array.Empty<Factura>(), Array.Empty<FacturaDetalle>()).Definicion;

            Assert.Equal("utilidad-por-periodo", d.Codigo);
            Assert.Equal("Utilidad por período", d.Nombre);
            Assert.Equal("Venta neta, costo y ganancia por mes, año o día. Las notas de crédito restan de la venta.", d.Descripcion);
            Assert.Equal("ReportesVentas", d.Modulo);
            Assert.Equal("Ventas", d.Familia);
            Assert.Equal(new[] { "fechas", "periodo", "cliente" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.True(d.Filtros.Single(f => f.Campo == "fechas").Obligatorio);
            var periodo = d.Filtros.Single(f => f.Campo == "periodo");
            Assert.Equal(TipoFiltro.Lista, periodo.Tipo);
            Assert.Equal("MES", periodo.ValorPorDefecto);
            Assert.Equal(new[] { "MES", "ANIO", "DIA" }, periodo.Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(new[] { "Mes", "Año", "Día" }, periodo.Opciones!.Select(o => o.Titulo).ToArray());
            Assert.Equal("C", d.Filtros.Single(f => f.Campo == "cliente").SubTipo);
            Assert.Equal(
                new[] { "periodo", "moneda", "ventas", "notasCredito", "ventaNeta", "costo", "utilidad" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.True(d.Columnas.Single(c => c.Campo == "periodo").Agrupable);
            Assert.Equal("Período", d.Columnas.Single(c => c.Campo == "periodo").Titulo);
            Assert.True(d.Columnas.Single(c => c.Campo == "moneda").Agrupable);
            Assert.False(d.Columnas.Single(c => c.Campo == "moneda").VisiblePorDefecto);
            foreach (var c in new[] { "ventas", "notasCredito", "ventaNeta", "costo", "utilidad" })
            {
                Assert.Equal(TipoColumna.Dinero, d.Columnas.Single(x => x.Campo == c).Tipo);
                Assert.Equal(Totalizacion.Suma, d.Columnas.Single(x => x.Campo == c).Totaliza);
            }
            Assert.Equal("Ventas netas", d.Columnas.Single(c => c.Campo == "ventas").Titulo);
            Assert.Equal("Notas de crédito", d.Columnas.Single(c => c.Campo == "notasCredito").Titulo);
            Assert.Equal("Venta neta total", d.Columnas.Single(c => c.Campo == "ventaNeta").Titulo);
            Assert.Equal("Utilidad", d.Columnas.Single(c => c.Campo == "utilidad").Titulo);
        }

        [Fact]
        public async Task Ejecutar_EjemploNumericoDosMesesConNotaDeCredito()
        {
            // Enero: venta 1,000, costo 600 → utilidad 400. Febrero: venta 500, costo 300, NC 100 → venta neta 400, utilidad 100.
            var r = Crear(
                new[] { Fac(1, Enero10), Fac(2, Febrero15) },
                new[] { Lin(1, 1, 1000m), Lin(2, 1, 500m) },
                new[] { Mov(1, 1, 600m), Mov(2, 1, 300m) },
                new[] { Nc(1, Febrero15, 100m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "2026-01", "2026-02" }, Periodos(filas));
            var ene = filas[0];
            Assert.Equal(1000m, ene["ventas"]);
            Assert.Equal(0m, ene["notasCredito"]);
            Assert.Equal(1000m, ene["ventaNeta"]);
            Assert.Equal(600m, ene["costo"]);
            Assert.Equal(400m, ene["utilidad"]);
            Assert.Equal("GTQ", ene["moneda"]);
            var feb = filas[1];
            Assert.Equal(500m, feb["ventas"]);
            Assert.Equal(100m, feb["notasCredito"]);
            Assert.Equal(400m, feb["ventaNeta"]);
            Assert.Equal(300m, feb["costo"]);
            Assert.Equal(100m, feb["utilidad"]);
        }

        [Fact]
        public async Task Ejecutar_ElPeriodoPorDefectoEsMes()
        {
            var r = Crear(
                new[] { Fac(1, new DateTime(2026, 1, 5)), Fac(2, new DateTime(2026, 1, 25)) },
                new[] { Lin(1, 1, 100m), Lin(2, 1, 200m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal("2026-01", f["periodo"]);
            Assert.Equal(300m, f["ventas"]);
        }

        [Fact]
        public async Task Ejecutar_AgrupaPorAnio()
        {
            var r = Crear(
                new[] { Fac(1, new DateTime(2025, 11, 5)), Fac(2, new DateTime(2025, 12, 20)), Fac(3, new DateTime(2026, 3, 1)) },
                new[] { Lin(1, 1, 100m), Lin(2, 1, 200m), Lin(3, 1, 50m) },
                notas: new[] { Nc(1, new DateTime(2026, 4, 1), 20m) });

            var filas = await r.EjecutarAsync(
                Filtros("2025-01-01", "2026-12-31", d => d["periodo"] = new FiltroValor { Valor = "ANIO" }), 100);

            Assert.Equal(new[] { "2025", "2026" }, Periodos(filas));
            Assert.Equal(300m, filas[0]["ventas"]);
            Assert.Equal(50m, filas[1]["ventas"]);
            Assert.Equal(20m, filas[1]["notasCredito"]);
            Assert.Equal(30m, filas[1]["ventaNeta"]);
        }

        [Fact]
        public async Task Ejecutar_AgrupaPorDiaYOrdenaPorFecha()
        {
            var r = Crear(
                new[] { Fac(1, new DateTime(2026, 1, 10)), Fac(2, new DateTime(2026, 1, 9)), Fac(3, new DateTime(2026, 1, 10)) },
                new[] { Lin(1, 1, 100m), Lin(2, 1, 40m), Lin(3, 1, 60m) },
                notas: new[] { Nc(1, new DateTime(2026, 1, 9), 5m) });

            var filas = await r.EjecutarAsync(Filtros(ajustar: d => d["periodo"] = new FiltroValor { Valor = "DIA" }), 100);

            Assert.Equal(new[] { "2026-01-09", "2026-01-10" }, Periodos(filas));
            Assert.Equal(40m, filas[0]["ventas"]);
            Assert.Equal(5m, filas[0]["notasCredito"]);
            Assert.Equal(160m, filas[1]["ventas"]);
        }

        [Fact]
        public async Task Ejecutar_PeriodoConSoloNotasDeCreditoApareceConVentasCeroYUtilidadNegativa()
        {
            var r = Crear(
                new[] { Fac(1, Enero10) },
                new[] { Lin(1, 1, 1000m) },
                new[] { Mov(1, 1, 600m) },
                new[] { Nc(1, Febrero15, 100m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "2026-01", "2026-02" }, Periodos(filas));
            var feb = filas[1];
            Assert.Equal(0m, feb["ventas"]);
            Assert.Equal(100m, feb["notasCredito"]);
            Assert.Equal(-100m, feb["ventaNeta"]);
            Assert.Equal(0m, feb["costo"]);
            Assert.Equal(-100m, feb["utilidad"]);
        }

        [Fact]
        public async Task Ejecutar_NotaDeCreditoCanceladaNoResta()
        {
            var r = Crear(
                new[] { Fac(1, Enero10) },
                new[] { Lin(1, 1, 1000m) },
                notas: new[] { Nc(1, Enero10, 100m, cancelado: "S"), Nc(2, Febrero15, 50m, cancelado: "S") });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal("2026-01", f["periodo"]);
            Assert.Equal(0m, f["notasCredito"]);
            Assert.Equal(1000m, f["ventaNeta"]);
        }

        [Fact]
        public async Task Ejecutar_FacturaCanceladaNoCuenta()
        {
            var r = Crear(new[] { Fac(1, cancelado: "S"), Fac(2) }, new[] { Lin(1, 1, 900m), Lin(2, 1, 100m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(100m, f["ventas"]);
        }

        [Fact]
        public async Task Ejecutar_DosMonedasSonFilasSeparadasPorPeriodo()
        {
            var r = Crear(
                new[] { Fac(1, moneda: "GTQ"), Fac(2, moneda: "USD") },
                new[] { Lin(1, 1, 100m), Lin(2, 1, 20m) },
                notas: new[] { Nc(1, Enero10, 10m, moneda: "USD") });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(2, filas.Count);
            var gtq = filas.Single(f => (string?)f["moneda"] == "GTQ");
            var usd = filas.Single(f => (string?)f["moneda"] == "USD");
            Assert.Equal(100m, gtq["ventaNeta"]);
            Assert.Equal(0m, gtq["notasCredito"]);
            Assert.Equal(10m, usd["ventaNeta"]);
            Assert.Equal(10m, usd["notasCredito"]);
        }

        [Fact]
        public async Task Ejecutar_FiltroDeClienteAplicaAVentasYANotasDeCredito()
        {
            var r = Crear(
                new[] { Fac(1, sn: "C001"), Fac(2, sn: "C002") },
                new[] { Lin(1, 1, 100m), Lin(2, 1, 700m) },
                notas: new[] { Nc(1, Enero10, 10m, sn: "C001"), Nc(2, Enero10, 300m, sn: "C002") });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(ajustar: d => d["cliente"] = new FiltroValor { Valor = "C001" }), 100));

            Assert.Equal(100m, f["ventas"]);
            Assert.Equal(10m, f["notasCredito"]);
            Assert.Equal(90m, f["ventaNeta"]);
        }

        [Fact]
        public async Task Ejecutar_LasFechasLimitanVentasYNotas_ElUltimoDiaEsInclusivo()
        {
            var r = Crear(
                new[] { Fac(1, new DateTime(2025, 12, 31)), Fac(2, new DateTime(2026, 1, 31)), Fac(3, new DateTime(2026, 2, 1)) },
                new[] { Lin(1, 1, 1m), Lin(2, 1, 10m), Lin(3, 1, 100m) },
                notas: new[] { Nc(1, new DateTime(2025, 12, 31), 1000m), Nc(2, new DateTime(2026, 1, 31), 5m), Nc(3, new DateTime(2026, 2, 1), 2000m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros("2026-01-01", "2026-01-31"), 100));

            Assert.Equal(10m, f["ventas"]);
            Assert.Equal(5m, f["notasCredito"]);
        }

        [Fact]
        public async Task Ejecutar_ElIvaNoAlteraElResultado()
        {
            var sinIva = Crear(
                new[] { Fac(1) }, new[] { Lin(1, 1, 1000m, impuesto: 0m) }, new[] { Mov(1, 1, 600m) },
                new[] { Nc(1, Enero10, 100m, iva: 0m) });
            var conIva = Crear(
                new[] { Fac(1) }, new[] { Lin(1, 1, 1000m, impuesto: 120m) }, new[] { Mov(1, 1, 600m) },
                new[] { Nc(1, Enero10, 100m, iva: 12m) });

            var a = Assert.Single(await sinIva.EjecutarAsync(Filtros(), 100));
            var b = Assert.Single(await conIva.EjecutarAsync(Filtros(), 100));

            foreach (var campo in new[] { "ventas", "notasCredito", "ventaNeta", "costo", "utilidad" })
                Assert.Equal(a[campo], b[campo]);
            Assert.Equal(300m, b["utilidad"]);
        }

        [Fact]
        public async Task Ejecutar_NotaDeDebitoPorConceptoSumaALasVentasConCostoCero()
        {
            var r = Crear(new[] { Fac(1), NdConcepto(2, Enero10, 50m) }, new[] { Lin(1, 1, 1000m) }, new[] { Mov(1, 1, 600m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(1050m, f["ventas"]);
            Assert.Equal(1050m, f["ventaNeta"]);
            Assert.Equal(600m, f["costo"]);
            Assert.Equal(450m, f["utilidad"]);
        }

        [Fact]
        public async Task Ejecutar_NotaDeDebitoYNotaDeCreditoPorConceptoDelMismoMesDanVentaNetaCero()
        {
            var r = Crear(
                new[] { NdConcepto(1, Enero10, 100m) }, Array.Empty<FacturaDetalle>(),
                notas: new[] { Nc(1, Enero10, 100m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(100m, f["ventas"]);
            Assert.Equal(100m, f["notasCredito"]);
            Assert.Equal(0m, f["ventaNeta"]);
            Assert.Equal(0m, f["utilidad"]);
        }

        [Fact]
        public async Task Ejecutar_NotaDeDebitoPorConceptoCanceladaONoConceptoNoSuma()
        {
            var porArticulos = NdConcepto(2, Enero10, 70m);
            porArticulos.DocType = "I";
            var r = Crear(new[] { NdConcepto(1, Enero10, 100m, cancelado: "S"), porArticulos }, Array.Empty<FacturaDetalle>());

            Assert.Empty(await r.EjecutarAsync(Filtros(), 100));
        }

        [Fact]
        public async Task Ejecutar_NotaDeDebitoPorConceptoRespetaFechasYCliente()
        {
            var r = Crear(
                new[]
                {
                    NdConcepto(1, new DateTime(2025, 12, 31), 1000m), NdConcepto(2, new DateTime(2026, 1, 31), 5m),
                    NdConcepto(3, new DateTime(2026, 2, 1), 2000m), NdConcepto(4, Enero10, 9m, sn: "C002")
                },
                Array.Empty<FacturaDetalle>());

            var f = Assert.Single(await r.EjecutarAsync(
                Filtros("2026-01-01", "2026-01-31", d => d["cliente"] = new FiltroValor { Valor = "C001" }), 100));

            Assert.Equal(5m, f["ventas"]);
        }

        [Fact]
        public async Task Ejecutar_ElIvaDeLaNotaDeDebitoPorConceptoNoAlteraElResultado()
        {
            var sinIva = Crear(new[] { NdConcepto(1, Enero10, 100m, iva: 0m) }, Array.Empty<FacturaDetalle>());
            var conIva = Crear(new[] { NdConcepto(1, Enero10, 100m, iva: 12m) }, Array.Empty<FacturaDetalle>());

            var a = Assert.Single(await sinIva.EjecutarAsync(Filtros(), 100));
            var b = Assert.Single(await conIva.EjecutarAsync(Filtros(), 100));

            Assert.Equal(100m, a["ventas"]);
            Assert.Equal(a["ventas"], b["ventas"]);
        }

        [Fact]
        public async Task Ejecutar_RespetaElLimite()
        {
            var r = Crear(
                new[] { Fac(1, new DateTime(2026, 1, 5)), Fac(2, new DateTime(2026, 2, 5)), Fac(3, new DateTime(2026, 3, 5)) },
                new[] { Lin(1, 1, 1m), Lin(2, 1, 1m), Lin(3, 1, 1m) });

            var filas = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(new[] { "2026-01", "2026-02" }, Periodos(filas));
        }
    }
}
