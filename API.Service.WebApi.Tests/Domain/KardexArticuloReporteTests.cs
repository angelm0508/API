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
    public class KardexArticuloReporteTests
    {
        private readonly Mock<IRepositorioGenerico<MovimientoInventario, int>> _movimientos = new();

        private KardexArticuloReporte Crear(params MovimientoInventario[] movimientos)
        {
            _movimientos.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(movimientos.AsAsyncQueryable());
            return new KardexArticuloReporte(_movimientos.Object);
        }

        private static MovimientoInventario Mov(int entry, DateTime fecha, decimal entra = 0m, decimal sale = 0m, string art = "A1", string alm = "01",
            string tipoDoc = "6", int docEntry = 1, int docLinea = 0, decimal costo = 10m, int? reversaDe = null) =>
            new()
            {
                Entry = entry, Fecha = fecha, CantidadEntra = entra, CantidadSale = sale, CodArticulo = art, CodAlmacen = alm,
                TipoDoc = tipoDoc, DocEntry = docEntry, DocLinea = docLinea, CostoUnitario = costo,
                ValorMovimiento = (entra - sale) * costo, MovReversaDe = reversaDe
            };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor> { ["articulo"] = new FiltroValor { Valor = "A1" } };
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        private static readonly DateTime E1 = new(2026, 1, 1);
        private static readonly DateTime E10 = new(2026, 1, 10);
        private static readonly DateTime E20 = new(2026, 1, 20);
        private static readonly DateTime F5 = new(2026, 2, 5);

        [Fact]
        public void Definicion_EsDeInventarioConArticuloObligatorio()
        {
            var d = Crear().Definicion;

            Assert.Equal("kardex-articulo", d.Codigo);
            Assert.Equal("Kardex por artículo", d.Nombre);
            Assert.Equal("Movimientos de un artículo con saldo acumulado.", d.Descripcion);
            Assert.Equal("ReportesInventario", d.Modulo);
            Assert.Equal("Inventario", d.Familia);
            Assert.Equal(new[] { "articulo", "fechas", "almacen" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal(TipoFiltro.Articulo, d.Filtros.Single(f => f.Campo == "articulo").Tipo);
            Assert.True(d.Filtros.Single(f => f.Campo == "articulo").Obligatorio);
            Assert.False(d.Filtros.Single(f => f.Campo == "fechas").Obligatorio);
            Assert.Equal(TipoFiltro.RangoFechas, d.Filtros.Single(f => f.Campo == "fechas").Tipo);
            Assert.Equal(TipoFiltro.Almacen, d.Filtros.Single(f => f.Campo == "almacen").Tipo);
            Assert.Equal(
                new[] { "fecha", "tipoDoc", "docEntry", "linea", "almacen", "entra", "sale", "costoUnitario", "valor", "saldo", "reversa" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal("Documento", d.Columnas.Single(c => c.Campo == "tipoDoc").Titulo);
            Assert.True(d.Columnas.Single(c => c.Campo == "tipoDoc").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "almacen").Agrupable);
            Assert.Equal("Entry del documento", d.Columnas.Single(c => c.Campo == "docEntry").Titulo);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "entra").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "sale").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "valor").Totaliza);
            Assert.Equal("Saldo acumulado", d.Columnas.Single(c => c.Campo == "saldo").Titulo);
            Assert.Equal(Totalizacion.Ninguna, d.Columnas.Single(c => c.Campo == "saldo").Totaliza);
            Assert.Equal("Costo", d.Columnas.Single(c => c.Campo == "costoUnitario").Titulo);
            Assert.Equal("Reversa", d.Columnas.Single(c => c.Campo == "reversa").Titulo);
        }

        [Fact]
        public async Task Motor_SinArticulo_LanzaElMensajeDeFiltroObligatorio()
        {
            var r = Crear();

            var ex = await Assert.ThrowsAsync<ReporteInvalidoException>(() => new MotorReportes().EjecutarAsync(r, new SolicitudReporte()));

            Assert.Equal("El filtro 'Artículo' es obligatorio.", ex.Message);
        }

        [Fact]
        public async Task Ejecutar_SaldoAcumuladoConEntradasSalidasYReversa()
        {
            var r = Crear(
                Mov(1, E1, entra: 10m, tipoDoc: "59", docEntry: 7, costo: 5m),
                Mov(2, E10, sale: 4m, tipoDoc: "6", docEntry: 20, docLinea: 1, costo: 5m),
                Mov(3, E20, entra: 4m, tipoDoc: "6", docEntry: 20, docLinea: 1, costo: 5m, reversaDe: 2));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(3, filas.Count);
            Assert.Equal(new[] { 10m, 6m, 10m }, filas.Select(f => (decimal)f["saldo"]!).ToArray());
            Assert.Equal("Entrada de mercancía", filas[0]["tipoDoc"]);
            Assert.Equal(7, filas[0]["docEntry"]);
            Assert.Equal(10m, filas[0]["entra"]);
            Assert.Equal(0m, filas[0]["sale"]);
            Assert.Equal(5m, filas[0]["costoUnitario"]);
            Assert.Equal(50m, filas[0]["valor"]);
            Assert.Equal("Factura / Nota de débito", filas[1]["tipoDoc"]);
            Assert.Equal(1, filas[1]["linea"]);
            Assert.Equal(4m, filas[1]["sale"]);
            Assert.Equal(-20m, filas[1]["valor"]);
            Assert.Equal("01", filas[1]["almacen"]);
            Assert.Equal(E10, filas[1]["fecha"]);
        }

        [Fact]
        public async Task Ejecutar_ReversaDiceSiEsUnaReversa()
        {
            var r = Crear(
                Mov(1, E1, sale: 4m),
                Mov(2, E10, entra: 4m, reversaDe: 1));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal("", filas[0]["reversa"]);
            Assert.Equal("Sí", filas[1]["reversa"]);
        }

        [Fact]
        public async Task Ejecutar_SaldoInicialEsLaSumaNetaAnteriorADesde()
        {
            var r = Crear(
                Mov(1, E1, entra: 100m),
                Mov(2, E1.AddDays(1), sale: 30m),
                Mov(3, E10, entra: 5m),
                Mov(4, E20, sale: 2m));

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10", Hasta = "2026-01-31" }), 100);

            Assert.Equal(3, filas.Count);
            Assert.Equal("Saldo inicial", filas[0]["tipoDoc"]);
            Assert.Equal(70m, filas[0]["saldo"]);
            Assert.Equal(0m, filas[0]["entra"]);
            Assert.Equal(0m, filas[0]["sale"]);
            Assert.Equal(0m, filas[0]["valor"]);
            Assert.Equal(75m, filas[1]["saldo"]);
            Assert.Equal(73m, filas[2]["saldo"]);
        }

        [Fact]
        public async Task Ejecutar_SaldoInicialNegativoTambienSeMuestra()
        {
            var r = Crear(Mov(1, E1, sale: 8m), Mov(2, E10, entra: 3m));

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10" }), 100);

            Assert.Equal(-8m, filas[0]["saldo"]);
            Assert.Equal(-5m, filas[1]["saldo"]);
        }

        [Fact]
        public async Task Ejecutar_SaldoInicialEnCero_NoAgregaFila()
        {
            var r = Crear(Mov(1, E1, entra: 5m), Mov(2, E1.AddDays(1), sale: 5m), Mov(3, E10, entra: 2m));

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10" }), 100);

            var f = Assert.Single(filas);
            Assert.Equal(2m, f["saldo"]);
        }

        [Fact]
        public async Task Ejecutar_SinDesde_NoHaySaldoInicialYAcumulaDesdeElPrimerMovimiento()
        {
            var r = Crear(Mov(1, E1, entra: 5m), Mov(2, E20, entra: 1m));

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Hasta = "2026-01-15" }), 100);

            var f = Assert.Single(filas);
            Assert.Equal(5m, f["saldo"]);
        }

        [Fact]
        public async Task Ejecutar_HastaIncluyeTodoElDiaFinal()
        {
            var r = Crear(Mov(1, E10.AddHours(15), entra: 5m), Mov(2, E10.AddDays(1), entra: 1m));

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10", Hasta = "2026-01-10" }), 100);

            var f = Assert.Single(filas);
            Assert.Equal(5m, f["entra"]);
        }

        [Fact]
        public async Task Ejecutar_FiltroDeAlmacenAfectaInicialYAcumulado()
        {
            var r = Crear(
                Mov(1, E1, entra: 100m, alm: "01"),
                Mov(2, E1, entra: 7m, alm: "02"),
                Mov(3, E10, entra: 5m, alm: "01"),
                Mov(4, E10, entra: 3m, alm: "02"));

            var filas = await r.EjecutarAsync(Filtros(d =>
            {
                d["fechas"] = new FiltroValor { Desde = "2026-01-10" };
                d["almacen"] = new FiltroValor { Valor = "02" };
            }), 100);

            Assert.Equal(2, filas.Count);
            Assert.Equal("Saldo inicial", filas[0]["tipoDoc"]);
            Assert.Equal(7m, filas[0]["saldo"]);
            Assert.Equal("02", filas[0]["almacen"]);
            Assert.Equal(10m, filas[1]["saldo"]);
            Assert.Equal("02", filas[1]["almacen"]);
        }

        [Fact]
        public async Task Ejecutar_SinFiltroDeAlmacen_SumaTodosLosAlmacenes()
        {
            var r = Crear(Mov(1, E1, entra: 100m, alm: "01"), Mov(2, E10, entra: 7m, alm: "02"));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { 100m, 107m }, filas.Select(f => (decimal)f["saldo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_SoloElArticuloPedido()
        {
            var r = Crear(Mov(1, E1, entra: 100m, art: "A2"), Mov(2, E10, entra: 7m, art: "A1"), Mov(3, E1, entra: 50m, art: "A2"));

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-05" }), 100);

            var f = Assert.Single(filas);
            Assert.Equal(7m, f["saldo"]);
        }

        [Fact]
        public async Task Ejecutar_OrdenPorFechaYLuegoEntryYAcumulaEnEseOrden()
        {
            var r = Crear(
                Mov(5, E10, entra: 1m),
                Mov(3, E10, entra: 10m),
                Mov(9, E1, entra: 100m),
                Mov(4, F5, sale: 11m));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { 100m, 110m, 111m, 100m }, filas.Select(f => (decimal)f["saldo"]!).ToArray());
            Assert.Equal(new[] { E1, E10, E10, F5 }, filas.Select(f => (DateTime)f["fecha"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_ElLimiteRecortaMovimientosPeroElAcumuladoSigueSiendoCorrecto()
        {
            var r = Crear(Mov(1, E1, entra: 100m), Mov(2, E10, entra: 5m), Mov(3, E20, entra: 7m));

            var filas = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(new[] { 100m, 105m }, filas.Select(f => (decimal)f["saldo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_ElLimiteCuentaLaFilaDeSaldoInicial_NuncaDevuelveMasFilasQueElLimite()
        {
            var r = Crear(Mov(1, E1, entra: 100m), Mov(2, E10, entra: 5m), Mov(3, E20, entra: 7m));

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10" }), 2);

            Assert.Equal(new[] { "Saldo inicial", "Factura / Nota de débito" }, filas.Select(f => (string)f["tipoDoc"]!).ToArray());
            Assert.Equal(new[] { 100m, 105m }, filas.Select(f => (decimal)f["saldo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_ConLimiteUnoYSaldoInicialSoloSeDevuelveElSaldoInicial()
        {
            var r = Crear(Mov(1, E1, entra: 100m), Mov(2, E10, entra: 5m));

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10" }), 1);

            var fila = Assert.Single(filas);
            Assert.Equal("Saldo inicial", fila["tipoDoc"]);
        }

        [Fact]
        public async Task Ejecutar_ConLimiteUnoSinSaldoInicialDevuelveUnMovimiento()
        {
            var r = Crear(Mov(1, E1, entra: 100m), Mov(2, E10, entra: 5m));

            var filas = await r.EjecutarAsync(Filtros(), 1);

            Assert.Single(filas);
            Assert.Equal(100m, filas[0]["saldo"]);
        }
    }
}