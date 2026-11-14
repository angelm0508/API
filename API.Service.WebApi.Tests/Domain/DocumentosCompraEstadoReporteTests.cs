using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class DocumentosCompraEstadoReporteTests
    {
        private static readonly DateTime Dia1 = new(2026, 1, 10, 9, 30, 0);
        private static readonly DateTime Dia2 = new(2026, 1, 31, 14, 0, 0);
        private static readonly DateTime Dia3 = new(2026, 2, 1, 8, 0, 0);

        private DocumentosCompraEstadoReporte Crear(
            PedidoCompra[]? pedidos = null, EntregaCompra[]? entregas = null, FacturaCompra[]? facturas = null)
        {
            var p = new Mock<IRepositorioGenerico<PedidoCompra, int>>();
            p.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((pedidos ?? Array.Empty<PedidoCompra>()).AsAsyncQueryable());
            var e = new Mock<IRepositorioGenerico<EntregaCompra, int>>();
            e.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((entregas ?? Array.Empty<EntregaCompra>()).AsAsyncQueryable());
            var f = new Mock<IRepositorioGenerico<FacturaCompra, int>>();
            f.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((facturas ?? Array.Empty<FacturaCompra>()).AsAsyncQueryable());
            return new DocumentosCompraEstadoReporte(p.Object, e.Object, f.Object);
        }

        private static PedidoCompra Ped(int n, DateTime fecha, string cancelado = "N", string estado = "O", string proveedor = "P1", decimal total = 200m) =>
            new() { Entry = n, NumDoc = n, Serie = 1, FechaDoc = fecha, Cancelado = cancelado, EstadoDoc = estado, CodigoSn = proveedor, NombreSn = "Proveedor " + proveedor, MonedaDoc = "QTZ", TotalDoc = total };

        private static EntregaCompra Ent(int n, DateTime fecha, string cancelado = "N", string estado = "O", string proveedor = "P1", decimal total = 300m) =>
            new() { Entry = n, NumDoc = n, Serie = 1, FechaDoc = fecha, Cancelado = cancelado, EstadoDoc = estado, CodigoSn = proveedor, NombreSn = "Proveedor " + proveedor, MonedaDoc = "QTZ", TotalDoc = total };

        private static FacturaCompra Fac(int n, DateTime fecha, string sub = "--", string cancelado = "N", string estado = "O", string proveedor = "P1", decimal total = 400m) =>
            new() { Entry = n, NumDoc = n, Serie = 1, FechaDoc = fecha, DocSubType = sub, Cancelado = cancelado, EstadoDoc = estado, CodigoSn = proveedor, NombreSn = "Proveedor " + proveedor, MonedaDoc = "QTZ", TotalDoc = total };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor>();
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        [Fact]
        public void Definicion_EsDeComprasConSusFiltrosYColumnas()
        {
            var d = Crear().Definicion;

            Assert.Equal("documentos-compra-estado", d.Codigo);
            Assert.Equal("Documentos de compra por estado", d.Nombre);
            Assert.Equal("Pedidos, entregas y facturas de compra con su estado (abierto, cerrado o cancelado).", d.Descripcion);
            Assert.Equal("Compras", d.Familia);
            Assert.Equal("ReportesCompras", d.Modulo);
            Assert.Equal(new[] { "fechas", "proveedor", "tipo", "estado" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal(TipoFiltro.RangoFechas, d.Filtros.Single(f => f.Campo == "fechas").Tipo);
            Assert.Equal(TipoFiltro.Socio, d.Filtros.Single(f => f.Campo == "proveedor").Tipo);
            Assert.Equal("P", d.Filtros.Single(f => f.Campo == "proveedor").SubTipo);
            var tipo = d.Filtros.Single(f => f.Campo == "tipo");
            Assert.Equal(TipoFiltro.Lista, tipo.Tipo);
            Assert.Equal(new[] { "PED", "ENT", "FAC" }, tipo.Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(new[] { "Pedido de compra", "Entrega de compra", "Factura de compra" }, tipo.Opciones!.Select(o => o.Titulo).ToArray());
            Assert.Equal(new[] { "ABIERTO", "CERRADO", "CANCELADO" }, d.Filtros.Single(f => f.Campo == "estado").Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(
                new[] { "tipo", "fecha", "numDoc", "codigoSn", "nombreSn", "estado", "moneda", "total" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal("Proveedor", d.Columnas.Single(c => c.Campo == "nombreSn").Titulo);
            Assert.Equal("Código proveedor", d.Columnas.Single(c => c.Campo == "codigoSn").Titulo);
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
                new[] { Ped(1, Dia1, cancelado, estadoDoc) }, new[] { Ent(1, Dia1, cancelado, estadoDoc) },
                new[] { Fac(1, Dia1, "--", cancelado, estadoDoc) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(3, filas.Count);
            Assert.All(filas, f => Assert.Equal(esperado, f["estado"]));
        }

        [Fact]
        public async Task Ejecutar_EstadoNuloEsAbierto()
        {
            var ped = Ped(1, Dia1);
            ped.Cancelado = null; ped.EstadoDoc = null;
            var r = Crear(new[] { ped });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal("Abierto", filas[0]["estado"]);
        }

        [Fact]
        public async Task Ejecutar_CadaTipoMuestraSuEtiquetaYSusDatos()
        {
            var r = Crear(new[] { Ped(2, Dia1, total: 22m) }, new[] { Ent(3, Dia1, total: 33m) }, new[] { Fac(4, Dia1, total: 44m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "Entrega de compra", "Factura de compra", "Pedido de compra" }, filas.Select(f => (string)f["tipo"]!).ToArray());
            Assert.Equal(22m, filas.Single(f => (string)f["tipo"]! == "Pedido de compra")["total"]);
            Assert.Equal(33m, filas.Single(f => (string)f["tipo"]! == "Entrega de compra")["total"]);
            var factura = filas.Single(f => (string)f["tipo"]! == "Factura de compra");
            Assert.Equal(44m, factura["total"]);
            Assert.Equal(4, factura["numDoc"]);
            Assert.Equal("P1", factura["codigoSn"]);
            Assert.Equal("Proveedor P1", factura["nombreSn"]);
            Assert.Equal("QTZ", factura["moneda"]);
            Assert.Equal(Dia1, factura["fecha"]);
        }

        [Fact]
        public async Task Ejecutar_LasFacturasIncluyenNotasDeDebito()
        {
            var r = Crear(facturas: new[] { Fac(1, Dia1, "--"), Fac(2, Dia1, "DN"), Fac(3, Dia1, "XX") });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
            Assert.All(filas, f => Assert.Equal("Factura de compra", f["tipo"]));
        }

        [Fact]
        public async Task Ejecutar_IncluyeLosCanceladosPorDefecto()
        {
            var r = Crear(new[] { Ped(1, Dia1), Ped(2, Dia1, cancelado: "S") });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "Abierto", "Cancelado" }, filas.Select(f => (string)f["estado"]!).ToArray());
        }

        [Theory]
        [InlineData("PED", "Pedido de compra")]
        [InlineData("ENT", "Entrega de compra")]
        [InlineData("FAC", "Factura de compra")]
        public async Task Ejecutar_FiltroTipo(string tipo, string esperado)
        {
            var r = Crear(new[] { Ped(1, Dia1) }, new[] { Ent(1, Dia1) }, new[] { Fac(1, Dia1) });

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
                Ped(1, Dia1, "N", "O"), Ped(2, Dia1, "N", "C"), Ped(3, Dia1, "S", "O"), Ped(4, Dia1, "S", "C")
            });

            var filas = await r.EjecutarAsync(Filtros(d => d["estado"] = new FiltroValor { Valor = estado }), 100);

            Assert.Equal(esperados, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_FiltroProveedor()
        {
            var r = Crear(new[] { Ped(1, Dia1, proveedor: "P1") }, new[] { Ent(2, Dia1, proveedor: "P2") }, new[] { Fac(3, Dia1, proveedor: "P2") });

            var filas = await r.EjecutarAsync(Filtros(d => d["proveedor"] = new FiltroValor { Valor = "P2" }), 100);

            Assert.Equal(new[] { "Entrega de compra", "Factura de compra" }, filas.Select(f => (string)f["tipo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_RangoDeFechas_EsInclusivoEnAmbosExtremos()
        {
            var r = Crear(new[] { Ped(1, Dia1), Ped(2, Dia2), Ped(3, Dia3) }, null, new[] { Fac(4, Dia3) });

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10", Hasta = "2026-01-31" }), 100);

            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_OrdenPorFechaTipoYNumeroYLimiteAlFinal()
        {
            var r = Crear(new[] { Ped(5, Dia2), Ped(2, Dia1) }, new[] { Ent(3, Dia1) }, new[] { Fac(4, Dia1) });

            var todas = await r.EjecutarAsync(Filtros(), 100);
            var limitadas = await r.EjecutarAsync(Filtros(), 3);

            Assert.Equal(new[] { "Entrega de compra", "Factura de compra", "Pedido de compra", "Pedido de compra" }, todas.Select(f => (string)f["tipo"]!).ToArray());
            Assert.Equal(new[] { 3, 4, 2 }, limitadas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_TotalNuloCuentaComoCero()
        {
            var ped = Ped(1, Dia1);
            ped.TotalDoc = null;
            var r = Crear(new[] { ped });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(0m, filas[0]["total"]);
        }
    }
}
