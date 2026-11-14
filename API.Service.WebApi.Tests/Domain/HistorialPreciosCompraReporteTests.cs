using API.Domain.Core.Reportes;
using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class HistorialPreciosCompraReporteTests
    {
        private static readonly DateTime Enero10 = new(2026, 1, 10);

        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _facturas = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>> _detalles = new();

        private HistorialPreciosCompraReporte Crear(IEnumerable<FacturaCompra> facturas, IEnumerable<FacturaCompraDetalle> detalles)
        {
            _facturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            _detalles.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(detalles.AsAsyncQueryable());
            return new HistorialPreciosCompraReporte(_facturas.Object, _detalles.Object);
        }

        private static FacturaCompra Fac(int entry, DateTime? fecha = null, string cancelado = "N", string subTipo = "--", string sn = "P001",
            string nombreSn = "Proveedor Uno", string moneda = "GTQ", int numDoc = 0) =>
            new()
            {
                Entry = entry, NumDoc = numDoc == 0 ? entry * 10 : numDoc, Serie = 1, Cancelado = cancelado, DocSubType = subTipo,
                FechaDoc = fecha ?? Enero10, CodigoSn = sn, NombreSn = nombreSn, MonedaDoc = moneda
            };

        private static FacturaCompraDetalle Lin(int entry, int noLinea, string? art = "A1", decimal cantidad = 2m, decimal precio = 50m,
            decimal factor = 1m, decimal desc = 0m, decimal? cantidadBase = null) =>
            new()
            {
                Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cantidad, Precio = precio, FactorUom = factor,
                PrctjeDesc = desc, CantidadBase = cantidadBase ?? cantidad * factor
            };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor> { ["articulo"] = new FiltroValor { Valor = "A1" } };
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        [Fact]
        public void Definicion_EsDeComprasConArticuloObligatorio()
        {
            var d = Crear(Array.Empty<FacturaCompra>(), Array.Empty<FacturaCompraDetalle>()).Definicion;

            Assert.Equal("historial-precios-compra", d.Codigo);
            Assert.Equal("Historial de precios de compra", d.Nombre);
            Assert.Equal("Precio neto pagado por un artículo a lo largo del tiempo.", d.Descripcion);
            Assert.Equal("Compras", d.Familia);
            Assert.Equal("ReportesCompras", d.Modulo);
            Assert.Equal(new[] { "articulo", "fechas", "proveedor" }, d.Filtros.Select(f => f.Campo).ToArray());
            var articulo = d.Filtros.Single(f => f.Campo == "articulo");
            Assert.Equal(TipoFiltro.Articulo, articulo.Tipo);
            Assert.True(articulo.Obligatorio);
            Assert.Equal(TipoFiltro.RangoFechas, d.Filtros.Single(f => f.Campo == "fechas").Tipo);
            Assert.False(d.Filtros.Single(f => f.Campo == "fechas").Obligatorio);
            var proveedor = d.Filtros.Single(f => f.Campo == "proveedor");
            Assert.Equal(TipoFiltro.Socio, proveedor.Tipo);
            Assert.Equal("P", proveedor.SubTipo);
            Assert.Equal(
                new[] { "fecha", "numDoc", "codigoSn", "nombreSn", "articulo", "moneda", "cantidad", "precioNeto", "descuento" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal(TipoColumna.Fecha, d.Columnas.Single(c => c.Campo == "fecha").Tipo);
            Assert.Equal(TipoColumna.Entero, d.Columnas.Single(c => c.Campo == "numDoc").Tipo);
            Assert.Equal("No.", d.Columnas.Single(c => c.Campo == "numDoc").Titulo);
            Assert.False(d.Columnas.Single(c => c.Campo == "codigoSn").VisiblePorDefecto);
            Assert.False(d.Columnas.Single(c => c.Campo == "moneda").VisiblePorDefecto);
            Assert.Equal("Proveedor", d.Columnas.Single(c => c.Campo == "nombreSn").Titulo);
            Assert.True(d.Columnas.Single(c => c.Campo == "nombreSn").Agrupable);
            Assert.Equal("Cantidad (base)", d.Columnas.Single(c => c.Campo == "cantidad").Titulo);
            Assert.Equal(TipoColumna.Cantidad, d.Columnas.Single(c => c.Campo == "cantidad").Tipo);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "cantidad").Totaliza);
            Assert.Equal("Precio neto por unidad base", d.Columnas.Single(c => c.Campo == "precioNeto").Titulo);
            Assert.Equal(TipoColumna.Dinero, d.Columnas.Single(c => c.Campo == "precioNeto").Tipo);
            Assert.Equal(Totalizacion.Ninguna, d.Columnas.Single(c => c.Campo == "precioNeto").Totaliza);
            Assert.Equal("Descuento %", d.Columnas.Single(c => c.Campo == "descuento").Titulo);
            Assert.Equal(TipoColumna.Cantidad, d.Columnas.Single(c => c.Campo == "descuento").Tipo);
        }

        [Fact]
        public async Task Motor_SinArticulo_LanzaElMensajeDeFiltroObligatorio()
        {
            var r = Crear(Array.Empty<FacturaCompra>(), Array.Empty<FacturaCompraDetalle>());

            var ex = await Assert.ThrowsAsync<ReporteInvalidoException>(() => new MotorReportes().EjecutarAsync(r, new SolicitudReporte()));

            Assert.Equal("El filtro 'Artículo' es obligatorio.", ex.Message);
        }

        [Fact]
        public async Task Ejecutar_UnaFilaPorLineaConSusDatos()
        {
            var r = Crear(new[] { Fac(1, numDoc: 501) }, new[] { Lin(1, 1, cantidad: 4m, precio: 25m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(Enero10, f["fecha"]);
            Assert.Equal(501, f["numDoc"]);
            Assert.Equal("P001", f["codigoSn"]);
            Assert.Equal("Proveedor Uno", f["nombreSn"]);
            Assert.Equal("A1", f["articulo"]);
            Assert.Equal("GTQ", f["moneda"]);
            Assert.Equal(4m, f["cantidad"]);
            Assert.Equal(25m, f["precioNeto"]);
            Assert.Equal(0m, f["descuento"]);
        }

        [Fact]
        public async Task Ejecutar_PrecioPorUnidadBaseConFactorDeUnidadDeMedida()
        {
            // 2 cajas de 12 unidades a 120 por caja: 24 unidades base a 10 por unidad.
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, cantidad: 2m, precio: 120m, factor: 12m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(24m, f["cantidad"]);
            Assert.Equal(10m, f["precioNeto"]);
        }

        [Fact]
        public async Task Ejecutar_AplicaElDescuentoDeLaLinea()
        {
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, precio: 100m, desc: 10m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(90m, f["precioNeto"]);
            Assert.Equal(10m, f["descuento"]);
        }

        [Fact]
        public async Task Ejecutar_FactorYDescuentoJuntos()
        {
            // 120 por caja de 12 = 10 por unidad base; con 25% de descuento = 7.5.
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, cantidad: 3m, precio: 120m, factor: 12m, desc: 25m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(36m, f["cantidad"]);
            Assert.Equal(7.5m, f["precioNeto"]);
        }

        [Fact]
        public async Task Ejecutar_RedondeaElPrecioASeisDecimales()
        {
            // 100 / 3 = 33.333333 (6 decimales) y con 10% de descuento = 29.9999997 -> 30.
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, precio: 100m, factor: 3m, desc: 10m) });

            var f = Assert.Single(await r.EjecutarAsync(Filtros(), 100));

            Assert.Equal(30m, f["precioNeto"]);
        }

        [Fact]
        public async Task Ejecutar_ValoresNulosCuentanComoCeroYSinCantidadBaseUsaLaCantidad()
        {
            var linea = Lin(1, 1, cantidad: 4m, precio: 10m);
            linea.CantidadBase = null; linea.PrctjeDesc = null;
            var sinPrecio = Lin(1, 2, cantidad: 1m);
            sinPrecio.Precio = null;
            var r = Crear(new[] { Fac(1) }, new[] { linea, sinPrecio });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(4m, filas[0]["cantidad"]);
            Assert.Equal(10m, filas[0]["precioNeto"]);
            Assert.Equal(0m, filas[0]["descuento"]);
            Assert.Equal(0m, filas[1]["precioNeto"]);
        }

        [Fact]
        public async Task Ejecutar_ExcluyeCanceladosOtrosSubtiposYOtrosArticulos()
        {
            var r = Crear(
                new[] { Fac(1, cancelado: "S"), Fac(2, subTipo: "XX"), Fac(3), Fac(4, subTipo: "DN") },
                new[] { Lin(1, 1), Lin(2, 1), Lin(3, 1), Lin(3, 2, art: "A2"), Lin(4, 1) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { 30, 40 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_OrdenPorFechaEntryYLinea()
        {
            var r = Crear(
                new[] { Fac(2, fecha: Enero10), Fac(1, fecha: Enero10.AddDays(1)), Fac(3, fecha: Enero10) },
                new[] { Lin(1, 1, precio: 4m), Lin(2, 2, precio: 2m), Lin(2, 1, precio: 1m), Lin(3, 1, precio: 3m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { 1m, 2m, 3m, 4m }, filas.Select(f => (decimal)f["precioNeto"]!).ToArray());
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
        public async Task Ejecutar_RespetaElLimite()
        {
            var r = Crear(new[] { Fac(1) }, new[] { Lin(1, 3, precio: 3m), Lin(1, 1, precio: 1m), Lin(1, 2, precio: 2m) });

            var filas = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(new[] { 1m, 2m }, filas.Select(f => (decimal)f["precioNeto"]!).ToArray());
        }
    }
}
