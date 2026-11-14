using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class NotasCreditoDebitoReporteTests
    {
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFacturas = new();
        private readonly Mock<IRepositorioGenerico<NotaCredito, int>> _repoNotas = new();

        private NotasCreditoDebitoReporte Crear(Factura[] facturas, NotaCredito[] notas)
        {
            _repoFacturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            _repoNotas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(notas.AsAsyncQueryable());
            return new NotasCreditoDebitoReporte(_repoFacturas.Object, _repoNotas.Object);
        }

        private static Factura Fac(int entry, DateTime fecha, string cliente = "C1", string sub = "--", string cancelado = "N",
            decimal total = 112m, int? baseTipo = null, int? baseEntry = null, int? numDoc = null, string moneda = "QTZ") =>
            new()
            {
                Entry = entry, NumDoc = numDoc ?? entry, Serie = 1, Cancelado = cancelado, DocSubType = sub, FechaDoc = fecha,
                CodigoSn = cliente, NombreSn = "Cliente " + cliente, MonedaDoc = moneda, TotalDoc = total,
                BaseTipo = baseTipo, BaseEntry = baseEntry
            };

        private static NotaCredito Nc(int entry, DateTime fecha, string cliente = "C1", string cancelado = "N", decimal total = 56m,
            int? baseTipo = null, int? baseEntry = null, int? numDoc = null) =>
            new()
            {
                Entry = entry, NumDoc = numDoc ?? entry, Serie = 2, Cancelado = cancelado, FechaDoc = fecha,
                CodigoSn = cliente, NombreSn = "Cliente " + cliente, MonedaDoc = "QTZ", TotalDoc = total,
                BaseTipo = baseTipo, BaseEntry = baseEntry
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

            Assert.Equal("notas-credito-debito", d.Codigo);
            Assert.Equal("Notas de crédito y débito emitidas", d.Nombre);
            Assert.Equal("Ventas", d.Familia);
            Assert.Equal("ReportesVentas", d.Modulo);
            Assert.Equal(new[] { "fechas", "cliente", "tipo" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal("C", d.Filtros.Single(f => f.Campo == "cliente").SubTipo);
            Assert.Equal(new[] { "NC", "ND" }, d.Filtros.Single(f => f.Campo == "tipo").Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(
                new[] { "fecha", "tipo", "serie", "numDoc", "codigoSn", "nombreSn", "facturaBase", "moneda", "total" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "total").Totaliza);
            Assert.Equal(TipoColumna.Entero, d.Columnas.Single(c => c.Campo == "facturaBase").Tipo);
            Assert.True(d.Columnas.Single(c => c.Campo == "tipo").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "nombreSn").Agrupable);
            Assert.False(d.Columnas.Single(c => c.Campo == "serie").VisiblePorDefecto);
            Assert.False(d.Columnas.Single(c => c.Campo == "codigoSn").VisiblePorDefecto);
        }

        [Fact]
        public async Task Ejecutar_NotaDeCreditoRestaYNotaDeDebitoSuma()
        {
            var r = Crear(
                new[] { Fac(1, Dia1, total: 500m), Fac(2, Dia1, sub: "ND", total: 56m, baseTipo: 6, baseEntry: 1) },
                new[] { Nc(1, Dia1, total: 80m, baseTipo: 6, baseEntry: 1) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(2, filas.Count);
            var nc = filas.Single(f => (string)f["tipo"]! == "Nota de crédito");
            Assert.Equal(-80m, nc["total"]);
            Assert.Equal("C1", nc["codigoSn"]);
            Assert.Equal("Cliente C1", nc["nombreSn"]);
            Assert.Equal("QTZ", nc["moneda"]);
            Assert.Equal(2, nc["serie"]);
            var nd = filas.Single(f => (string)f["tipo"]! == "Nota de débito");
            Assert.Equal(56m, nd["total"]);
        }

        [Fact]
        public async Task Ejecutar_FacturaBaseSeResuelveParaNotaDeCreditoYDeDebito()
        {
            var r = Crear(
                new[]
                {
                    Fac(10, Dia1, numDoc: 1001),
                    Fac(11, Dia1, numDoc: 1002),
                    Fac(20, Dia2, sub: "ND", baseTipo: 6, baseEntry: 11, numDoc: 5)
                },
                new[] { Nc(30, Dia2, baseTipo: 6, baseEntry: 10, numDoc: 7) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(1001, filas.Single(f => (string)f["tipo"]! == "Nota de crédito")["facturaBase"]);
            Assert.Equal(1002, filas.Single(f => (string)f["tipo"]! == "Nota de débito")["facturaBase"]);
        }

        [Fact]
        public async Task Ejecutar_SinBaseOBaseDeOtroTipoOInexistente_FacturaBaseVacia()
        {
            var r = Crear(
                new[]
                {
                    Fac(10, Dia1, numDoc: 1001),
                    Fac(20, Dia2, sub: "ND"),
                    Fac(21, Dia2, sub: "ND", baseTipo: 5, baseEntry: 10)
                },
                new[] { Nc(30, Dia2, baseTipo: -1), Nc(31, Dia2, baseTipo: 6, baseEntry: 999) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(4, filas.Count);
            Assert.All(filas, f => Assert.Null(f["facturaBase"]));
        }

        [Fact]
        public async Task Ejecutar_LaFacturaBaseSeResuelveConUnaSolaConsultaDeFacturas()
        {
            var r = Crear(
                new[] { Fac(10, Dia1, numDoc: 1001), Fac(11, Dia1, numDoc: 1002) },
                new[] { Nc(30, Dia2, baseTipo: 6, baseEntry: 10), Nc(31, Dia2, baseTipo: 6, baseEntry: 11), Nc(32, Dia2, baseTipo: 6, baseEntry: 10) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new int?[] { 1001, 1002, 1001 }, filas.Select(f => (int?)f["facturaBase"]).ToArray());
            // Una consulta para las notas de débito y otra (única) para resolver las facturas base.
            _repoFacturas.Verify(x => x.ObtenerTodoAsync(), Times.Exactly(2));
        }

        [Fact]
        public async Task Ejecutar_MuchasBases_SeResuelvenPorLotes()
        {
            var facturas = Enumerable.Range(1, 2500).Select(i => Fac(i, Dia1, numDoc: 10000 + i)).ToArray();
            var notas = Enumerable.Range(1, 2500).Select(i => Nc(i, Dia2, baseTipo: 6, baseEntry: i, numDoc: i)).ToArray();
            var r = Crear(facturas, notas);

            var filas = await r.EjecutarAsync(Filtros(), 10000);

            Assert.Equal(2500, filas.Count);
            Assert.All(filas, f => Assert.Equal(10000 + (int)f["numDoc"]!, f["facturaBase"]));
        }

        [Fact]
        public async Task Ejecutar_ExcluyeLosCanceladosYLasFacturasNormales()
        {
            var r = Crear(
                new[] { Fac(1, Dia1), Fac(2, Dia1, sub: "ND"), Fac(3, Dia1, sub: "ND", cancelado: "S") },
                new[] { Nc(1, Dia1), Nc(2, Dia1, cancelado: "S") });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(2, filas.Count);
            Assert.Equal(new[] { "Nota de crédito", "Nota de débito" }, filas.Select(f => (string)f["tipo"]!).ToArray());
        }

        [Theory]
        [InlineData("NC", new[] { "Nota de crédito" })]
        [InlineData("ND", new[] { "Nota de débito" })]
        public async Task Ejecutar_FiltroTipo(string tipo, string[] esperados)
        {
            var r = Crear(new[] { Fac(2, Dia1, sub: "ND") }, new[] { Nc(1, Dia1) });

            var filas = await r.EjecutarAsync(Filtros(d => d["tipo"] = new FiltroValor { Valor = tipo }), 100);

            Assert.Equal(esperados, filas.Select(f => (string)f["tipo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_FiltroCliente()
        {
            var r = Crear(new[] { Fac(2, Dia1, "C1", sub: "ND"), Fac(3, Dia1, "C2", sub: "ND") }, new[] { Nc(1, Dia1, "C1"), Nc(2, Dia1, "C2") });

            var filas = await r.EjecutarAsync(Filtros(d => d["cliente"] = new FiltroValor { Valor = "C2" }), 100);

            Assert.Equal(2, filas.Count);
            Assert.All(filas, f => Assert.Equal("C2", f["codigoSn"]));
        }

        [Fact]
        public async Task Ejecutar_RangoDeFechas_EsInclusivoEnAmbosExtremos()
        {
            var r = Crear(new[] { Fac(2, Dia2, sub: "ND"), Fac(3, Dia3, sub: "ND") }, new[] { Nc(1, Dia1), Nc(2, Dia3) });

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10", Hasta = "2026-01-31" }), 100);

            Assert.Equal(2, filas.Count);
            Assert.All(filas, f => Assert.True((DateTime)f["fecha"]! < Dia3));
        }

        [Fact]
        public async Task Ejecutar_OrdenNaturalYLimite()
        {
            var r = Crear(new[] { Fac(2, Dia2, sub: "ND") }, new[] { Nc(1, Dia1), Nc(2, Dia2) });

            var todas = await r.EjecutarAsync(Filtros(), 100);
            var limitadas = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(new[] { Dia1, Dia2, Dia2 }, todas.Select(f => (DateTime)f["fecha"]!).ToArray());
            Assert.Equal(new[] { "Nota de crédito", "Nota de crédito" }, limitadas.Select(f => (string)f["tipo"]!).ToArray());
            Assert.Equal(2, limitadas.Count);
        }

        [Fact]
        public async Task Ejecutar_TotalNuloCuentaComoCero()
        {
            var nota = Nc(1, Dia1);
            nota.TotalDoc = null;
            var r = Crear(Array.Empty<Factura>(), new[] { nota });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(0m, filas[0]["total"]);
        }
    }
}
