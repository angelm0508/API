using API.Domain.Core.Reportes;
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
    public class MovimientosInventarioReporteTests
    {
        private readonly Mock<IRepositorioGenerico<MovimientoInventario, int>> _movimientos = new();

        private MovimientosInventarioReporte Crear(params MovimientoInventario[] movimientos)
        {
            _movimientos.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(movimientos.AsAsyncQueryable());
            return new MovimientosInventarioReporte(_movimientos.Object);
        }

        private static MovimientoInventario Mov(int entry, DateTime fecha, decimal entra = 0m, decimal sale = 0m, string art = "A1", string alm = "01",
            string tipoDoc = "6", int docEntry = 1, int docLinea = 0, decimal costo = 10m) =>
            new()
            {
                Entry = entry, Fecha = fecha, CantidadEntra = entra, CantidadSale = sale, CodArticulo = art, CodAlmacen = alm,
                TipoDoc = tipoDoc, DocEntry = docEntry, DocLinea = docLinea, CostoUnitario = costo, ValorMovimiento = (entra - sale) * costo,
                CodArticuloNavigation = new Articulo { Codigo = art, Nombre = "Artículo " + art }
            };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor> { ["fechas"] = new FiltroValor { Desde = "2026-01-01", Hasta = "2026-01-31" } };
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        private static readonly DateTime E10 = new(2026, 1, 10);
        private static readonly DateTime E20 = new(2026, 1, 20);

        private static int[] Entries(IReadOnlyList<FilaReporte> filas) => filas.Select(f => (int)f["docEntry"]!).ToArray();

        [Fact]
        public void Definicion_EsDeInventarioConFechasObligatorias()
        {
            var d = Crear().Definicion;

            Assert.Equal("movimientos-inventario", d.Codigo);
            Assert.Equal("Movimientos por período", d.Nombre);
            Assert.Equal("Entradas y salidas de inventario con su valor.", d.Descripcion);
            Assert.Equal("ReportesInventario", d.Modulo);
            Assert.Equal("Inventario", d.Familia);
            Assert.Equal(new[] { "fechas", "articulo", "almacen", "tipoDoc" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.True(d.Filtros.Single(f => f.Campo == "fechas").Obligatorio);
            Assert.Equal(TipoFiltro.RangoFechas, d.Filtros.Single(f => f.Campo == "fechas").Tipo);
            Assert.Equal(TipoFiltro.Articulo, d.Filtros.Single(f => f.Campo == "articulo").Tipo);
            Assert.Equal(TipoFiltro.Almacen, d.Filtros.Single(f => f.Campo == "almacen").Tipo);
            var tipoDoc = d.Filtros.Single(f => f.Campo == "tipoDoc");
            Assert.Equal(TipoFiltro.Lista, tipoDoc.Tipo);
            Assert.Equal(
                TiposDocumentoInventario.Opciones.Select(o => $"{o.Valor}|{o.Titulo}").ToArray(),
                tipoDoc.Opciones!.Select(o => $"{o.Valor}|{o.Titulo}").ToArray());
            Assert.Equal(
                new[] { "fecha", "tipoDoc", "docEntry", "linea", "articulo", "nombre", "almacen", "entra", "sale", "costoUnitario", "valor" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.True(d.Columnas.Single(c => c.Campo == "tipoDoc").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "articulo").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "almacen").Agrupable);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "entra").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "sale").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "valor").Totaliza);
            Assert.Equal(TipoColumna.Dinero, d.Columnas.Single(c => c.Campo == "valor").Tipo);
        }

        [Fact]
        public async Task Motor_SinFechas_LanzaElMensajeDeFiltroObligatorio()
        {
            var r = Crear();

            var ex = await Assert.ThrowsAsync<ReporteInvalidoException>(() => new MotorReportes().EjecutarAsync(r, new SolicitudReporte()));

            Assert.Equal("El filtro 'Fechas' es obligatorio.", ex.Message);
        }

        [Fact]
        public async Task Ejecutar_ValorEsFirmado_LaSalidaEsNegativa()
        {
            var r = Crear(
                Mov(1, E10, entra: 10m, tipoDoc: "59", docEntry: 7, costo: 5m),
                Mov(2, E20, sale: 4m, tipoDoc: "6", docEntry: 20, docLinea: 2, costo: 5m));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(2, filas.Count);
            Assert.Equal("Entrada de mercancía", filas[0]["tipoDoc"]);
            Assert.Equal(10m, filas[0]["entra"]);
            Assert.Equal(0m, filas[0]["sale"]);
            Assert.Equal(50m, filas[0]["valor"]);
            Assert.Equal("Factura / Nota de débito", filas[1]["tipoDoc"]);
            Assert.Equal(20, filas[1]["docEntry"]);
            Assert.Equal(2, filas[1]["linea"]);
            Assert.Equal("A1", filas[1]["articulo"]);
            Assert.Equal("Artículo A1", filas[1]["nombre"]);
            Assert.Equal("01", filas[1]["almacen"]);
            Assert.Equal(0m, filas[1]["entra"]);
            Assert.Equal(4m, filas[1]["sale"]);
            Assert.Equal(5m, filas[1]["costoUnitario"]);
            Assert.Equal(-20m, filas[1]["valor"]);
            Assert.Equal(E20, filas[1]["fecha"]);
        }

        [Fact]
        public async Task Ejecutar_FiltroDeFechas_LimiteSuperiorInclusivoDelDiaFinal()
        {
            var r = Crear(
                Mov(1, new DateTime(2025, 12, 31, 23, 0, 0), entra: 1m, docEntry: 1),
                Mov(2, new DateTime(2026, 1, 1), entra: 1m, docEntry: 2),
                Mov(3, new DateTime(2026, 1, 31, 18, 0, 0), entra: 1m, docEntry: 3),
                Mov(4, new DateTime(2026, 2, 1), entra: 1m, docEntry: 4));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { 2, 3 }, Entries(filas));
        }

        [Fact]
        public async Task Ejecutar_FiltrosArticuloAlmacenYTipoDoc()
        {
            var r = Crear(
                Mov(1, E10, entra: 1m, art: "A1", alm: "01", tipoDoc: "6", docEntry: 1),
                Mov(2, E10, entra: 1m, art: "A2", alm: "01", tipoDoc: "6", docEntry: 2),
                Mov(3, E10, entra: 1m, art: "A1", alm: "02", tipoDoc: "59", docEntry: 3),
                Mov(4, E10, entra: 1m, art: "A1", alm: "01", tipoDoc: "59", docEntry: 4));

            var porArticulo = await r.EjecutarAsync(Filtros(d => d["articulo"] = new FiltroValor { Valor = "A2" }), 100);
            var porAlmacen = await r.EjecutarAsync(Filtros(d => d["almacen"] = new FiltroValor { Valor = "02" }), 100);
            var porTipo = await r.EjecutarAsync(Filtros(d => d["tipoDoc"] = new FiltroValor { Valor = "59" }), 100);
            var combinado = await r.EjecutarAsync(Filtros(d =>
            {
                d["articulo"] = new FiltroValor { Valor = "A1" };
                d["almacen"] = new FiltroValor { Valor = "01" };
                d["tipoDoc"] = new FiltroValor { Valor = "59" };
            }), 100);

            Assert.Equal(new[] { 2 }, Entries(porArticulo));
            Assert.Equal(new[] { 3 }, Entries(porAlmacen));
            Assert.Equal(new[] { 3, 4 }, Entries(porTipo));
            Assert.Equal(new[] { 4 }, Entries(combinado));
        }

        [Fact]
        public async Task Ejecutar_TipoDocDesconocidoMuestraDocumentoYElCodigo()
        {
            var r = Crear(Mov(1, E10, entra: 1m, tipoDoc: "99"));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal("Documento 99", filas[0]["tipoDoc"]);
        }

        [Fact]
        public async Task Ejecutar_OrdenPorFechaYLuegoEntryYAplicaElLimite()
        {
            var r = Crear(
                Mov(5, E20, entra: 1m, docEntry: 50),
                Mov(3, E10, entra: 1m, docEntry: 30),
                Mov(2, E10, entra: 1m, docEntry: 20),
                Mov(1, E20, entra: 1m, docEntry: 10));

            var todos = await r.EjecutarAsync(Filtros(), 100);
            var dos = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(new[] { 20, 30, 10, 50 }, Entries(todos));
            Assert.Equal(new[] { 20, 30 }, Entries(dos));
        }
    }
}
