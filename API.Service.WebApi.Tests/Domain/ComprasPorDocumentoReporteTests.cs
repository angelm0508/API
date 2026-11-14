using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ComprasPorDocumentoReporteTests
    {
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoFacturas = new();
        private readonly Mock<IRepositorioGenerico<NotaCreditoCompra, int>> _repoNotas = new();

        private ComprasPorDocumentoReporte Crear(FacturaCompra[] facturas, NotaCreditoCompra[] notas)
        {
            _repoFacturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            _repoNotas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(notas.AsAsyncQueryable());
            return new ComprasPorDocumentoReporte(_repoFacturas.Object, _repoNotas.Object);
        }

        private static FacturaCompra Fac(int entry, DateTime fecha, string proveedor = "P1", string sub = "--", string cancelado = "N",
            int serie = 1, decimal bruto = 100m, decimal desc = 0m, decimal imp = 12m, decimal total = 112m, string moneda = "QTZ") =>
            new()
            {
                Entry = entry, NumDoc = entry, Serie = serie, Cancelado = cancelado, DocSubType = sub, FechaDoc = fecha,
                CodigoSn = proveedor, NombreSn = "Proveedor " + proveedor, MonedaDoc = moneda,
                TotalBruto = bruto, TotalDesc = desc, TotalImp = imp, TotalDoc = total
            };

        private static NotaCreditoCompra Nc(int entry, DateTime fecha, string proveedor = "P1", string cancelado = "N",
            int serie = 2, decimal bruto = 50m, decimal imp = 6m, decimal total = 56m) =>
            new()
            {
                Entry = entry, NumDoc = entry, Serie = serie, Cancelado = cancelado, FechaDoc = fecha,
                CodigoSn = proveedor, NombreSn = "Proveedor " + proveedor, MonedaDoc = "QTZ",
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
        public void Definicion_EsDeComprasConSusFiltrosYColumnas()
        {
            var d = Crear(Array.Empty<FacturaCompra>(), Array.Empty<NotaCreditoCompra>()).Definicion;

            Assert.Equal("compras-por-documento", d.Codigo);
            Assert.Equal("Compras por documento", d.Nombre);
            Assert.Equal("ReportesCompras", d.Modulo);
            Assert.Equal("Compras", d.Familia);
            Assert.Equal(new[] { "fechas", "proveedor", "tipo", "serie" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal(TipoFiltro.RangoFechas, d.Filtros.Single(f => f.Campo == "fechas").Tipo);
            Assert.Equal(TipoFiltro.Socio, d.Filtros.Single(f => f.Campo == "proveedor").Tipo);
            Assert.Equal("P", d.Filtros.Single(f => f.Campo == "proveedor").SubTipo);
            Assert.Equal(TipoFiltro.Numero, d.Filtros.Single(f => f.Campo == "serie").Tipo);
            var tipo = d.Filtros.Single(f => f.Campo == "tipo");
            Assert.Equal(TipoFiltro.Lista, tipo.Tipo);
            Assert.Equal(new[] { "FAC", "DN", "NC" }, tipo.Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(new[] { "Factura de compra", "Nota de débito", "Nota de crédito" }, tipo.Opciones!.Select(o => o.Titulo).ToArray());
            Assert.Equal(
                new[] { "fecha", "tipo", "serie", "numDoc", "codigoSn", "nombreSn", "moneda", "bruto", "descuento", "impuesto", "total" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal("Código proveedor", d.Columnas.Single(c => c.Campo == "codigoSn").Titulo);
            Assert.Equal("Proveedor", d.Columnas.Single(c => c.Campo == "nombreSn").Titulo);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "total").Totaliza);
            Assert.True(d.Columnas.Single(c => c.Campo == "fecha").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "nombreSn").Agrupable);
            Assert.False(d.Columnas.Single(c => c.Campo == "serie").VisiblePorDefecto);
            Assert.False(d.Columnas.Single(c => c.Campo == "moneda").VisiblePorDefecto);
        }

        [Fact]
        public async Task Ejecutar_FacturasNotasDeDebitoYNotasDeCredito_ConSignoEnLasNotasDeCredito()
        {
            var r = Crear(
                new[] { Fac(1, Dia1), Fac(2, Dia1, sub: "DN", total: 56m, bruto: 50m, imp: 6m) },
                new[] { Nc(1, Dia1) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(3, filas.Count);
            var factura = filas.Single(f => (string)f["tipo"]! == "Factura de compra");
            Assert.Equal(112m, factura["total"]);
            Assert.Equal(100m, factura["bruto"]);
            Assert.Equal(12m, factura["impuesto"]);
            Assert.Equal("P1", factura["codigoSn"]);
            Assert.Equal("Proveedor P1", factura["nombreSn"]);
            Assert.Equal("QTZ", factura["moneda"]);
            Assert.Equal(56m, filas.Single(f => (string)f["tipo"]! == "Nota de débito de compra")["total"]);
            var nota = filas.Single(f => (string)f["tipo"]! == "Nota de crédito de compra");
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
            Assert.Equal("Factura de compra", filas[0]["tipo"]);
        }

        [Theory]
        [InlineData("FAC", new[] { "Factura de compra" })]
        [InlineData("DN", new[] { "Nota de débito de compra" })]
        [InlineData("NC", new[] { "Nota de crédito de compra" })]
        public async Task Ejecutar_FiltroTipo(string tipo, string[] esperados)
        {
            var r = Crear(new[] { Fac(1, Dia1), Fac(2, Dia1, sub: "DN") }, new[] { Nc(1, Dia1) });

            var filas = await r.EjecutarAsync(Filtros(d => d["tipo"] = new FiltroValor { Valor = tipo }), 100);

            Assert.Equal(esperados, filas.Select(f => (string)f["tipo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_FiltroProveedorYSerie()
        {
            var r = Crear(new[] { Fac(1, Dia1, "P1", serie: 1), Fac(2, Dia1, "P2", serie: 1), Fac(3, Dia1, "P1", serie: 9) }, new[] { Nc(1, Dia1, "P1", serie: 2) });

            var porProveedor = await r.EjecutarAsync(Filtros(d => d["proveedor"] = new FiltroValor { Valor = "P1" }), 100);
            var porSerie = await r.EjecutarAsync(Filtros(d => d["serie"] = new FiltroValor { Valor = "9" }), 100);

            Assert.Equal(3, porProveedor.Count);
            Assert.All(porProveedor, f => Assert.Equal("P1", f["codigoSn"]));
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
            Assert.Equal(new[] { "Factura de compra", "Nota de crédito de compra" }, filas.Select(f => (string)f["tipo"]!).ToArray());
            Assert.All(filas, f => Assert.Equal(Dia1, f["fecha"]));
        }

        [Fact]
        public async Task Ejecutar_ImportesNulosCuentanComoCero()
        {
            var factura = Fac(1, Dia1);
            factura.TotalBruto = null; factura.TotalDesc = null; factura.TotalImp = null; factura.TotalDoc = null;
            var r = Crear(new[] { factura }, Array.Empty<NotaCreditoCompra>());

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(0m, filas[0]["total"]);
            Assert.Equal(0m, filas[0]["bruto"]);
        }
    }
}
