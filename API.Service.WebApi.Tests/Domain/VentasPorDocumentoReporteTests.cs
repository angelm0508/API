using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class VentasPorDocumentoReporteTests
    {
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFacturas = new();
        private readonly Mock<IRepositorioGenerico<NotaCredito, int>> _repoNotas = new();

        private VentasPorDocumentoReporte Crear(Factura[] facturas, NotaCredito[] notas)
        {
            _repoFacturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            _repoNotas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(notas.AsAsyncQueryable());
            return new VentasPorDocumentoReporte(_repoFacturas.Object, _repoNotas.Object);
        }

        private static Factura Fac(int entry, DateTime fecha, string cliente = "C1", string sub = "--", string cancelado = "N",
            int serie = 1, decimal bruto = 100m, decimal desc = 0m, decimal imp = 12m, decimal total = 112m, string moneda = "QTZ") =>
            new()
            {
                Entry = entry, NumDoc = entry, Serie = serie, Cancelado = cancelado, DocSubType = sub, FechaDoc = fecha,
                CodigoSn = cliente, NombreSn = "Cliente " + cliente, MonedaDoc = moneda,
                TotalBruto = bruto, TotalDesc = desc, TotalImp = imp, TotalDoc = total
            };

        private static NotaCredito Nc(int entry, DateTime fecha, string cliente = "C1", string cancelado = "N",
            int serie = 2, decimal bruto = 50m, decimal imp = 6m, decimal total = 56m) =>
            new()
            {
                Entry = entry, NumDoc = entry, Serie = serie, Cancelado = cancelado, FechaDoc = fecha,
                CodigoSn = cliente, NombreSn = "Cliente " + cliente, MonedaDoc = "QTZ",
                TotalBruto = bruto, TotalDesc = 0m, TotalImp = imp, TotalDoc = total
            };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor>();
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        private static readonly DateTime Dia1 = new(2026, 1, 10, 9, 30, 0);
        private static readonly DateTime Dia2 = new(2026, 1, 31, 14, 0, 0);
        private static readonly DateTime Dia3 = new(2026, 2, 1, 8, 0, 0);

        [Fact]
        public void Definicion_EsDeVentasConSusFiltrosYColumnas()
        {
            var d = Crear(Array.Empty<Factura>(), Array.Empty<NotaCredito>()).Definicion;

            Assert.Equal("ventas-por-documento", d.Codigo);
            Assert.Equal("ReportesVentas", d.Modulo);
            Assert.Equal("Ventas", d.Familia);
            Assert.Equal(new[] { "fechas", "cliente", "tipo", "serie" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal("C", d.Filtros.Single(f => f.Campo == "cliente").SubTipo);
            Assert.Equal(new[] { "FAC", "ND", "NC" }, d.Filtros.Single(f => f.Campo == "tipo").Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "total").Totaliza);
            Assert.True(d.Columnas.Single(c => c.Campo == "fecha").Agrupable);
            Assert.False(d.Columnas.Single(c => c.Campo == "serie").VisiblePorDefecto);
        }

        [Fact]
        public async Task Ejecutar_FacturasNotasDeDebitoYNotasDeCredito_ConSignoEnLasNotasDeCredito()
        {
            var r = Crear(
                new[] { Fac(1, Dia1), Fac(2, Dia1, sub: "ND", total: 56m, bruto: 50m, imp: 6m) },
                new[] { Nc(1, Dia1) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(3, filas.Count);
            var factura = filas.Single(f => (string)f["tipo"]! == "Factura");
            Assert.Equal(112m, factura["total"]);
            Assert.Equal(100m, factura["bruto"]);
            Assert.Equal(12m, factura["impuesto"]);
            Assert.Equal("C1", factura["codigoSn"]);
            Assert.Equal("Cliente C1", factura["nombreSn"]);
            Assert.Equal("QTZ", factura["moneda"]);
            Assert.Equal(56m, filas.Single(f => (string)f["tipo"]! == "Nota de débito")["total"]);
            var nota = filas.Single(f => (string)f["tipo"]! == "Nota de crédito");
            Assert.Equal(-56m, nota["total"]);
            Assert.Equal(-50m, nota["bruto"]);
            Assert.Equal(-6m, nota["impuesto"]);
            Assert.Equal(0m, nota["descuento"]);
        }

        [Fact]
        public async Task Ejecutar_ExcluyeLosCancelados()
        {
            var r = Crear(new[] { Fac(1, Dia1), Fac(2, Dia1, cancelado: "S") }, new[] { Nc(1, Dia1, cancelado: "S") });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Single(filas);
            Assert.Equal("Factura", filas[0]["tipo"]);
        }

        [Theory]
        [InlineData("FAC", new[] { "Factura" })]
        [InlineData("ND", new[] { "Nota de débito" })]
        [InlineData("NC", new[] { "Nota de crédito" })]
        public async Task Ejecutar_FiltroTipo(string tipo, string[] esperados)
        {
            var r = Crear(new[] { Fac(1, Dia1), Fac(2, Dia1, sub: "ND") }, new[] { Nc(1, Dia1) });

            var filas = await r.EjecutarAsync(Filtros(d => d["tipo"] = new FiltroValor { Valor = tipo }), 100);

            Assert.Equal(esperados, filas.Select(f => (string)f["tipo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_FiltroClienteYSerie()
        {
            var r = Crear(new[] { Fac(1, Dia1, "C1", serie: 1), Fac(2, Dia1, "C2", serie: 1), Fac(3, Dia1, "C1", serie: 9) }, new[] { Nc(1, Dia1, "C1", serie: 2) });

            var porCliente = await r.EjecutarAsync(Filtros(d => d["cliente"] = new FiltroValor { Valor = "C1" }), 100);
            var porSerie = await r.EjecutarAsync(Filtros(d => d["serie"] = new FiltroValor { Valor = "9" }), 100);

            Assert.Equal(3, porCliente.Count);
            Assert.All(porCliente, f => Assert.Equal("C1", f["codigoSn"]));
            Assert.Single(porSerie);
            Assert.Equal(9, porSerie[0]["serie"]);
        }

        [Fact]
        public async Task Ejecutar_RangoDeFechas_EsInclusivoEnAmbosExtremos()
        {
            var r = Crear(new[] { Fac(1, Dia1), Fac(2, Dia2), Fac(3, Dia3) }, new[] { Nc(1, Dia3) });

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10", Hasta = "2026-01-31" }), 100);

            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_OrdenNaturalPorFechaYAplicaElLimite()
        {
            var r = Crear(new[] { Fac(1, Dia2), Fac(2, Dia1) }, new[] { Nc(1, Dia1) });

            var filas = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(2, filas.Count);
            Assert.Equal(new[] { "Factura", "Nota de crédito" }, filas.Select(f => (string)f["tipo"]!).ToArray());
            Assert.All(filas, f => Assert.Equal(Dia1, f["fecha"]));
        }

        [Fact]
        public async Task Ejecutar_ImportesNulosCuentanComoCero()
        {
            var factura = Fac(1, Dia1);
            factura.TotalBruto = null; factura.TotalDesc = null; factura.TotalImp = null; factura.TotalDoc = null;
            var r = Crear(new[] { factura }, Array.Empty<NotaCredito>());

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(0m, filas[0]["total"]);
            Assert.Equal(0m, filas[0]["bruto"]);
        }
    }
}
