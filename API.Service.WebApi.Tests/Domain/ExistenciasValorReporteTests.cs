using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ExistenciasValorReporteTests
    {
        private readonly Mock<IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>> _repo = new();

        private ExistenciasValorReporte Crear(params ExistenciaArticulo[] filas)
        {
            _repo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());
            return new ExistenciasValorReporte(_repo.Object);
        }

        private static ExistenciaArticulo Ex(string art, string alm, decimal disponible, string metodo = "P", decimal promedio = 10m,
            decimal estandar = 8m, string? grupo = "Lubricantes", decimal comprometido = 0m, decimal pedido = 0m) =>
            new()
            {
                CodArticulo = art, CodAlmacen = alm, Disponible = disponible, Comprometido = comprometido, Pedido = pedido,
                CodArticuloNavigation = new Articulo
                {
                    Codigo = art, Nombre = "Artículo " + art, MetodoValuacion = metodo, CostoPromedio = promedio, CostoEstandar = estandar,
                    CodigoGrupoNavigation = grupo is null ? null : new GrupoArticulo { Codigo = 100, Nombre = grupo }
                }
            };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor>();
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        [Fact]
        public void Definicion_EsDeInventarioConSusFiltrosYColumnas()
        {
            var d = Crear().Definicion;

            Assert.Equal("existencias-valor", d.Codigo);
            Assert.Equal("ReportesInventario", d.Modulo);
            Assert.Equal("Inventario", d.Familia);
            Assert.Equal(new[] { "almacen", "articulo", "grupo", "soloConExistencias" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal(TipoFiltro.Almacen, d.Filtros.Single(f => f.Campo == "almacen").Tipo);
            Assert.Equal("N", d.Filtros.Single(f => f.Campo == "soloConExistencias").ValorPorDefecto);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "valor").Totaliza);
            Assert.True(d.Columnas.Single(c => c.Campo == "almacen").Agrupable);
        }

        [Fact]
        public async Task Ejecutar_ValorEsEnStockPorCostoPromedio()
        {
            var r = Crear(Ex("A1", "01", 4m, promedio: 12.5m));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            var f = Assert.Single(filas);
            Assert.Equal("01", f["almacen"]);
            Assert.Equal("A1", f["articulo"]);
            Assert.Equal("Artículo A1", f["nombre"]);
            Assert.Equal("Lubricantes", f["grupo"]);
            Assert.Equal(4m, f["enStock"]);
            Assert.Equal(12.5m, f["costo"]);
            Assert.Equal(50m, f["valor"]);
        }

        [Fact]
        public async Task Ejecutar_ConMetodoEstandar_UsaElCostoEstandar()
        {
            var r = Crear(Ex("A1", "01", 3m, metodo: "E", promedio: 10m, estandar: 7m));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(7m, filas[0]["costo"]);
            Assert.Equal(21m, filas[0]["valor"]);
        }

        [Fact]
        public async Task Ejecutar_LlevaComprometidoYPedidoYTolerarGrupoNulo()
        {
            var r = Crear(Ex("A1", "01", 1m, grupo: null, comprometido: 2m, pedido: 5m));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Null(filas[0]["grupo"]);
            Assert.Equal(2m, filas[0]["comprometido"]);
            Assert.Equal(5m, filas[0]["pedido"]);
        }

        [Fact]
        public async Task Ejecutar_FiltrosAlmacenArticuloYGrupo()
        {
            var r = Crear(Ex("A1", "01", 1m), Ex("A1", "02", 1m), Ex("A2", "01", 1m, grupo: "Filtros"));

            var porAlmacen = await r.EjecutarAsync(Filtros(d => d["almacen"] = new FiltroValor { Valor = "02" }), 100);
            var porArticulo = await r.EjecutarAsync(Filtros(d => d["articulo"] = new FiltroValor { Valor = "A2" }), 100);
            var porGrupo = await r.EjecutarAsync(Filtros(d => d["grupo"] = new FiltroValor { Valor = "Filtro" }), 100);

            Assert.Equal(new[] { "A1" }, porAlmacen.Select(f => (string)f["articulo"]!).ToArray());
            Assert.Equal(new[] { "A2" }, porArticulo.Select(f => (string)f["articulo"]!).ToArray());
            Assert.Equal(new[] { "A2" }, porGrupo.Select(f => (string)f["articulo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_SoloConExistencias_ExcluyeLasFilasEnCero()
        {
            var r = Crear(Ex("A1", "01", 0m), Ex("A2", "01", 2m));

            var todas = await r.EjecutarAsync(Filtros(d => d["soloConExistencias"] = new FiltroValor { Valor = "N" }), 100);
            var soloCon = await r.EjecutarAsync(Filtros(d => d["soloConExistencias"] = new FiltroValor { Valor = "S" }), 100);

            Assert.Equal(2, todas.Count);
            Assert.Equal(new[] { "A2" }, soloCon.Select(f => (string)f["articulo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_OrdenNaturalPorAlmacenYArticuloYAplicaElLimite()
        {
            var r = Crear(Ex("B", "02", 1m), Ex("A", "02", 1m), Ex("B", "01", 1m));

            var filas = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(new[] { "01|B", "02|A" }, filas.Select(f => $"{f["almacen"]}|{f["articulo"]}").ToArray());
        }
    }
}
