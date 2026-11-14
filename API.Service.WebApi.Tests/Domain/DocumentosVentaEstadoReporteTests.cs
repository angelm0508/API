using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class DocumentosVentaEstadoReporteTests
    {
        private static readonly DateTime Dia1 = new(2026, 1, 10, 9, 30, 0);
        private static readonly DateTime Dia2 = new(2026, 1, 31, 14, 0, 0);
        private static readonly DateTime Dia3 = new(2026, 2, 1, 8, 0, 0);

        private DocumentosVentaEstadoReporte Crear(
            Cotizacion[]? cotizaciones = null, Pedido[]? pedidos = null, Entrega[]? entregas = null, Factura[]? facturas = null)
        {
            var c = new Mock<IRepositorioGenerico<Cotizacion, int>>();
            c.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((cotizaciones ?? Array.Empty<Cotizacion>()).AsAsyncQueryable());
            var p = new Mock<IRepositorioGenerico<Pedido, int>>();
            p.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((pedidos ?? Array.Empty<Pedido>()).AsAsyncQueryable());
            var e = new Mock<IRepositorioGenerico<Entrega, int>>();
            e.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((entregas ?? Array.Empty<Entrega>()).AsAsyncQueryable());
            var f = new Mock<IRepositorioGenerico<Factura, int>>();
            f.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((facturas ?? Array.Empty<Factura>()).AsAsyncQueryable());
            return new DocumentosVentaEstadoReporte(c.Object, p.Object, e.Object, f.Object);
        }

        private static Cotizacion Cot(int n, DateTime fecha, string cancelado = "N", string estado = "O", string cliente = "C1", decimal total = 100m) =>
            new() { Entry = n, NumDoc = n, Serie = 1, FechaDoc = fecha, Cancelado = cancelado, EstadoDoc = estado, CodigoSn = cliente, NombreSn = "Cliente " + cliente, MonedaDoc = "QTZ", TotalDoc = total };

        private static Pedido Ped(int n, DateTime fecha, string cancelado = "N", string estado = "O", string cliente = "C1", decimal total = 200m) =>
            new() { Entry = n, NumDoc = n, Serie = 1, FechaDoc = fecha, Cancelado = cancelado, EstadoDoc = estado, CodigoSn = cliente, NombreSn = "Cliente " + cliente, MonedaDoc = "QTZ", TotalDoc = total };

        private static Entrega Ent(int n, DateTime fecha, string cancelado = "N", string estado = "O", string cliente = "C1", decimal total = 300m) =>
            new() { Entry = n, NumDoc = n, Serie = 1, FechaDoc = fecha, Cancelado = cancelado, EstadoDoc = estado, CodigoSn = cliente, NombreSn = "Cliente " + cliente, MonedaDoc = "QTZ", TotalDoc = total };

        private static Factura Fac(int n, DateTime fecha, string sub = "--", string cancelado = "N", string estado = "O", string cliente = "C1", decimal total = 400m) =>
            new() { Entry = n, NumDoc = n, Serie = 1, FechaDoc = fecha, DocSubType = sub, Cancelado = cancelado, EstadoDoc = estado, CodigoSn = cliente, NombreSn = "Cliente " + cliente, MonedaDoc = "QTZ", TotalDoc = total };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor>();
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        [Fact]
        public void Definicion_EsDeVentasConSusFiltrosYColumnas()
        {
            var d = Crear().Definicion;

            Assert.Equal("documentos-venta-estado", d.Codigo);
            Assert.Equal("Documentos de venta por estado", d.Nombre);
            Assert.Equal("Ventas", d.Familia);
            Assert.Equal("ReportesVentas", d.Modulo);
            Assert.Equal(new[] { "fechas", "cliente", "tipo", "estado" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal("C", d.Filtros.Single(f => f.Campo == "cliente").SubTipo);
            Assert.Equal(new[] { "COT", "PED", "ENT", "FAC" }, d.Filtros.Single(f => f.Campo == "tipo").Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(new[] { "ABIERTO", "CERRADO", "CANCELADO" }, d.Filtros.Single(f => f.Campo == "estado").Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(
                new[] { "tipo", "fecha", "numDoc", "codigoSn", "nombreSn", "estado", "moneda", "total" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.True(d.Columnas.Single(c => c.Campo == "estado").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "tipo").Agrupable);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "total").Totaliza);
            Assert.False(d.Columnas.Single(c => c.Campo == "codigoSn").VisiblePorDefecto);
        }

        [Theory]
        [InlineData("N", "O", "Abierto")]
        [InlineData("N", "C", "Cerrado")]
        [InlineData("S", "O", "Cancelado")]
        [InlineData("S", "C", "Cancelado")]
        public async Task Ejecutar_EstadoPorCombinacionEnCadaTipo(string cancelado, string estadoDoc, string esperado)
        {
            var r = Crear(
                new[] { Cot(1, Dia1, cancelado, estadoDoc) }, new[] { Ped(1, Dia1, cancelado, estadoDoc) },
                new[] { Ent(1, Dia1, cancelado, estadoDoc) }, new[] { Fac(1, Dia1, "--", cancelado, estadoDoc) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(4, filas.Count);
            Assert.All(filas, f => Assert.Equal(esperado, f["estado"]));
        }

        [Fact]
        public async Task Ejecutar_EstadoNuloEsAbierto()
        {
            var cot = Cot(1, Dia1);
            cot.Cancelado = null; cot.EstadoDoc = null;
            var r = Crear(new[] { cot });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal("Abierto", filas[0]["estado"]);
        }

        [Fact]
        public async Task Ejecutar_CadaTipoMuestraSuEtiquetaYSusDatos()
        {
            var r = Crear(
                new[] { Cot(1, Dia1, total: 11m) }, new[] { Ped(2, Dia1, total: 22m) },
                new[] { Ent(3, Dia1, total: 33m) }, new[] { Fac(4, Dia1, total: 44m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "Cotización", "Entrega", "Factura", "Pedido" }, filas.Select(f => (string)f["tipo"]!).ToArray());
            Assert.Equal(11m, filas.Single(f => (string)f["tipo"]! == "Cotización")["total"]);
            Assert.Equal(22m, filas.Single(f => (string)f["tipo"]! == "Pedido")["total"]);
            Assert.Equal(33m, filas.Single(f => (string)f["tipo"]! == "Entrega")["total"]);
            var factura = filas.Single(f => (string)f["tipo"]! == "Factura");
            Assert.Equal(44m, factura["total"]);
            Assert.Equal(4, factura["numDoc"]);
            Assert.Equal("C1", factura["codigoSn"]);
            Assert.Equal("Cliente C1", factura["nombreSn"]);
            Assert.Equal("QTZ", factura["moneda"]);
            Assert.Equal(Dia1, factura["fecha"]);
        }

        [Fact]
        public async Task Ejecutar_LasFacturasIncluyenNotasDeDebito()
        {
            var r = Crear(facturas: new[] { Fac(1, Dia1, "--"), Fac(2, Dia1, "ND"), Fac(3, Dia1, "XX") });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
            Assert.All(filas, f => Assert.Equal("Factura", f["tipo"]));
        }

        [Fact]
        public async Task Ejecutar_IncluyeLosCanceladosPorDefecto()
        {
            var r = Crear(new[] { Cot(1, Dia1), Cot(2, Dia1, cancelado: "S") });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "Abierto", "Cancelado" }, filas.Select(f => (string)f["estado"]!).ToArray());
        }

        [Theory]
        [InlineData("COT", "Cotización")]
        [InlineData("PED", "Pedido")]
        [InlineData("ENT", "Entrega")]
        [InlineData("FAC", "Factura")]
        public async Task Ejecutar_FiltroTipo(string tipo, string esperado)
        {
            var r = Crear(new[] { Cot(1, Dia1) }, new[] { Ped(1, Dia1) }, new[] { Ent(1, Dia1) }, new[] { Fac(1, Dia1) });

            var filas = await r.EjecutarAsync(Filtros(d => d["tipo"] = new FiltroValor { Valor = tipo }), 100);

            Assert.Single(filas);
            Assert.Equal(esperado, filas[0]["tipo"]);
        }

        [Theory]
        [InlineData("ABIERTO", new[] { 1 })]
        [InlineData("CERRADO", new[] { 2 })]
        [InlineData("CANCELADO", new[] { 3, 4 })]
        public async Task Ejecutar_FiltroEstado_AplicaTambienALosCancelados(string estado, int[] esperados)
        {
            var r = Crear(new[]
            {
                Cot(1, Dia1, "N", "O"), Cot(2, Dia1, "N", "C"), Cot(3, Dia1, "S", "O"), Cot(4, Dia1, "S", "C")
            });

            var filas = await r.EjecutarAsync(Filtros(d => d["estado"] = new FiltroValor { Valor = estado }), 100);

            Assert.Equal(esperados, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_FiltroCliente()
        {
            var r = Crear(new[] { Cot(1, Dia1, cliente: "C1") }, new[] { Ped(2, Dia1, cliente: "C2") }, null, new[] { Fac(3, Dia1, cliente: "C2") });

            var filas = await r.EjecutarAsync(Filtros(d => d["cliente"] = new FiltroValor { Valor = "C2" }), 100);

            Assert.Equal(new[] { "Factura", "Pedido" }, filas.Select(f => (string)f["tipo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_RangoDeFechas_EsInclusivoEnAmbosExtremos()
        {
            var r = Crear(new[] { Cot(1, Dia1), Cot(2, Dia2), Cot(3, Dia3) }, null, null, new[] { Fac(4, Dia3) });

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10", Hasta = "2026-01-31" }), 100);

            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_OrdenPorFechaTipoYNumeroYLimiteAlFinal()
        {
            var r = Crear(
                new[] { Cot(5, Dia2), Cot(1, Dia1) }, new[] { Ped(2, Dia1) }, new[] { Ent(3, Dia1) }, new[] { Fac(4, Dia1) });

            var todas = await r.EjecutarAsync(Filtros(), 100);
            var limitadas = await r.EjecutarAsync(Filtros(), 3);

            Assert.Equal(new[] { "Cotización", "Entrega", "Factura", "Pedido", "Cotización" }, todas.Select(f => (string)f["tipo"]!).ToArray());
            Assert.Equal(new[] { 1, 3, 4 }, limitadas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_TotalNuloCuentaComoCero()
        {
            var cot = Cot(1, Dia1);
            cot.TotalDoc = null;
            var r = Crear(new[] { cot });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(0m, filas[0]["total"]);
        }
    }
}
