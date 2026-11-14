using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class StockBajoMinimoReporteTests
    {
        private readonly Mock<IRepositorioGenerico<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)>> _config = new();
        private readonly Mock<IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>> _existencias = new();

        private StockBajoMinimoReporte Crear(IEnumerable<ArticuloAlmacen> config, params ExistenciaArticulo[] existencias)
        {
            _config.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(config.AsAsyncQueryable());
            _existencias.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(existencias.AsAsyncQueryable());
            return new StockBajoMinimoReporte(_config.Object, _existencias.Object);
        }

        private static ArticuloAlmacen Cfg(string art, string alm, decimal? minimo, decimal? maximo, string bloqueado = "N", string? grupo = "Lubricantes") =>
            new()
            {
                CodArticulo = art, CodAlmacen = alm, Minimo = minimo, Maximo = maximo, Bloqueado = bloqueado,
                CodArticuloNavigation = new Articulo
                {
                    Codigo = art, Nombre = "Artículo " + art,
                    CodigoGrupoNavigation = grupo is null ? null : new GrupoArticulo { Codigo = 100, Nombre = grupo }
                }
            };

        private static ExistenciaArticulo Ex(string art, string alm, decimal disponible, decimal comprometido = 0m, decimal pedido = 0m) =>
            new() { CodArticulo = art, CodAlmacen = alm, Disponible = disponible, Comprometido = comprometido, Pedido = pedido };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor>();
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        private static string[] Articulos(IReadOnlyList<FilaReporte> filas) => filas.Select(f => (string)f["articulo"]!).ToArray();

        [Fact]
        public void Definicion_EsDeInventarioConSusFiltrosYColumnas()
        {
            var d = Crear(Array.Empty<ArticuloAlmacen>()).Definicion;

            Assert.Equal("stock-bajo-minimo", d.Codigo);
            Assert.Equal("Stock bajo mínimo o sobre máximo", d.Nombre);
            Assert.Equal("Artículos cuyo stock por almacén está por debajo del mínimo o por encima del máximo configurado.", d.Descripcion);
            Assert.Equal("ReportesInventario", d.Modulo);
            Assert.Equal("Inventario", d.Familia);
            Assert.Equal(new[] { "almacen", "grupo", "estado" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal(TipoFiltro.Almacen, d.Filtros.Single(f => f.Campo == "almacen").Tipo);
            Assert.Equal(TipoFiltro.Texto, d.Filtros.Single(f => f.Campo == "grupo").Tipo);
            var estado = d.Filtros.Single(f => f.Campo == "estado");
            Assert.Equal(TipoFiltro.Lista, estado.Tipo);
            Assert.Equal("FUERA", estado.ValorPorDefecto);
            Assert.Equal(
                new[] { "FUERA|Bajo mínimo o sobre máximo", "BAJO|Bajo mínimo", "SOBRE|Sobre máximo", "TODOS|Todos" },
                estado.Opciones!.Select(o => $"{o.Valor}|{o.Titulo}").ToArray());
            Assert.Equal(
                new[] { "almacen", "articulo", "nombre", "grupo", "enStock", "minimo", "maximo", "estado", "nivelNecesario" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.True(d.Columnas.Single(c => c.Campo == "almacen").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "grupo").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "estado").Agrupable);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "enStock").Totaliza);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "nivelNecesario").Totaliza);
            Assert.Equal("Por pedir", d.Columnas.Single(c => c.Campo == "nivelNecesario").Titulo);
            Assert.Equal("En stock", d.Columnas.Single(c => c.Campo == "enStock").Titulo);
        }

        private StockBajoMinimoReporte CrearConLosTresEstados() =>
            Crear(
                new[]
                {
                    Cfg("BAJO", "01", 10m, 100m),
                    Cfg("SOBRE", "01", 10m, 100m),
                    Cfg("NORMAL", "01", 10m, 100m)
                },
                Ex("BAJO", "01", 5m), Ex("SOBRE", "01", 150m), Ex("NORMAL", "01", 50m));

        [Fact]
        public async Task Ejecutar_PorDefectoMuestraBajoMinimoYSobreMaximo()
        {
            var r = CrearConLosTresEstados();

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "BAJO", "SOBRE" }, Articulos(filas));
            Assert.Equal("Bajo mínimo", filas[0]["estado"]);
            Assert.Equal("Sobre máximo", filas[1]["estado"]);
        }

        [Fact]
        public async Task Ejecutar_FiltroEstado_BajoSobreYTodos()
        {
            var r = CrearConLosTresEstados();

            var bajo = await r.EjecutarAsync(Filtros(d => d["estado"] = new FiltroValor { Valor = "BAJO" }), 100);
            var sobre = await r.EjecutarAsync(Filtros(d => d["estado"] = new FiltroValor { Valor = "SOBRE" }), 100);
            var todos = await r.EjecutarAsync(Filtros(d => d["estado"] = new FiltroValor { Valor = "TODOS" }), 100);
            var fuera = await r.EjecutarAsync(Filtros(d => d["estado"] = new FiltroValor { Valor = "FUERA" }), 100);

            Assert.Equal(new[] { "BAJO" }, Articulos(bajo));
            Assert.Equal(new[] { "SOBRE" }, Articulos(sobre));
            Assert.Equal(new[] { "BAJO", "NORMAL", "SOBRE" }, Articulos(todos));
            Assert.Equal("Normal", todos[1]["estado"]);
            Assert.Equal(new[] { "BAJO", "SOBRE" }, Articulos(fuera));
        }

        [Fact]
        public async Task Ejecutar_LosLimitesExactosSonNormales()
        {
            var r = Crear(
                new[] { Cfg("EnMinimo", "01", 10m, 100m), Cfg("EnMaximo", "01", 10m, 100m) },
                Ex("EnMinimo", "01", 10m), Ex("EnMaximo", "01", 100m));

            var filas = await r.EjecutarAsync(Filtros(d => d["estado"] = new FiltroValor { Valor = "TODOS" }), 100);

            Assert.All(filas, f => Assert.Equal("Normal", f["estado"]));
        }

        [Fact]
        public async Task Ejecutar_CeroSignificaSinNivel()
        {
            var r = Crear(
                new[]
                {
                    Cfg("SoloMaximo", "01", 0m, 5m),     // sin mínimo: nunca está bajo mínimo; 100 > 5 -> sobre máximo
                    Cfg("SoloMinimo", "01", 10m, 0m),    // sin máximo: nunca está sobre máximo; 100 >= 10 -> normal
                    Cfg("NullMinimo", "01", null, 5m),
                    Cfg("SinNiveles", "01", 0m, 0m),     // no entra a la fuente
                    Cfg("SinNivelesNull", "01", null, null)
                },
                Ex("SoloMaximo", "01", 100m), Ex("SoloMinimo", "01", 100m), Ex("NullMinimo", "01", 100m),
                Ex("SinNiveles", "01", 100m), Ex("SinNivelesNull", "01", 100m));

            var filas = await r.EjecutarAsync(Filtros(d => d["estado"] = new FiltroValor { Valor = "TODOS" }), 100);

            Assert.Equal(new[] { "NullMinimo", "SoloMaximo", "SoloMinimo" }, Articulos(filas));
            Assert.Equal("Sobre máximo", filas[0]["estado"]);
            Assert.Equal(0m, filas[0]["minimo"]);
            Assert.Equal("Sobre máximo", filas[1]["estado"]);
            Assert.Equal("Normal", filas[2]["estado"]);
            Assert.Equal(0m, filas[2]["maximo"]);
            Assert.Equal(0m, filas[2]["nivelNecesario"]);   // sin máximo no hay nivel necesario
        }

        [Fact]
        public async Task Ejecutar_ArticuloSinFilaDeExistencia_TieneStockCeroYEstaBajoMinimo()
        {
            var r = Crear(new[] { Cfg("A1", "01", 5m, 0m) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            var f = Assert.Single(filas);
            Assert.Equal(0m, f["enStock"]);
            Assert.Equal("Bajo mínimo", f["estado"]);
        }

        [Fact]
        public async Task Ejecutar_ArticuloSinExistenciaYSoloMaximo_NoEstaFuera()
        {
            var r = Crear(new[] { Cfg("A1", "01", 0m, 5m) });

            var fuera = await r.EjecutarAsync(Filtros(), 100);
            var todos = await r.EjecutarAsync(Filtros(d => d["estado"] = new FiltroValor { Valor = "TODOS" }), 100);

            Assert.Empty(fuera);
            Assert.Equal("Normal", Assert.Single(todos)["estado"]);
            Assert.Equal(5m, todos[0]["nivelNecesario"]);
        }

        [Fact]
        public async Task Ejecutar_NivelNecesarioEsMaximoMenosDisponibleConComprometidoYPedido()
        {
            // disponible = 30 - 10 + 5 = 25 -> por pedir = 100 - 25 = 75
            var r = Crear(new[] { Cfg("A1", "01", 40m, 100m) }, Ex("A1", "01", 30m, comprometido: 10m, pedido: 5m));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            var f = Assert.Single(filas);
            Assert.Equal(30m, f["enStock"]);
            Assert.Equal(40m, f["minimo"]);
            Assert.Equal(100m, f["maximo"]);
            Assert.Equal(75m, f["nivelNecesario"]);
        }

        [Fact]
        public async Task Ejecutar_NivelNecesarioNuncaEsNegativo()
        {
            var r = Crear(new[] { Cfg("A1", "01", 0m, 10m) }, Ex("A1", "01", 150m));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal("Sobre máximo", filas[0]["estado"]);
            Assert.Equal(0m, filas[0]["nivelNecesario"]);
        }

        [Fact]
        public async Task Ejecutar_LaExistenciaSeUneArticuloYAlmacen()
        {
            var r = Crear(
                new[] { Cfg("A1", "01", 10m, 0m), Cfg("A1", "02", 10m, 0m) },
                Ex("A1", "01", 50m), Ex("A1", "02", 3m), Ex("A2", "02", 1m));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            var f = Assert.Single(filas);
            Assert.Equal("02", f["almacen"]);
            Assert.Equal(3m, f["enStock"]);
        }

        [Fact]
        public async Task Ejecutar_ArticulosBloqueadosSiguenApareciendo()
        {
            var r = Crear(new[] { Cfg("A1", "01", 10m, 100m, bloqueado: "S") }, Ex("A1", "01", 1m));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "A1" }, Articulos(filas));
        }

        [Fact]
        public async Task Ejecutar_LlevaNombreYGrupoYTolerarGrupoNulo()
        {
            var r = Crear(new[] { Cfg("A1", "01", 10m, 0m), Cfg("A2", "01", 10m, 0m, grupo: null) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal("Artículo A1", filas[0]["nombre"]);
            Assert.Equal("Lubricantes", filas[0]["grupo"]);
            Assert.Null(filas[1]["grupo"]);
        }

        [Fact]
        public async Task Ejecutar_FiltrosAlmacenYGrupo()
        {
            var r = Crear(new[]
            {
                Cfg("A1", "01", 10m, 0m), Cfg("A1", "02", 10m, 0m), Cfg("A2", "01", 10m, 0m, grupo: "Filtros"), Cfg("A3", "01", 10m, 0m, grupo: null)
            });

            var porAlmacen = await r.EjecutarAsync(Filtros(d => d["almacen"] = new FiltroValor { Valor = "02" }), 100);
            var porGrupo = await r.EjecutarAsync(Filtros(d => d["grupo"] = new FiltroValor { Valor = "Filtro" }), 100);

            Assert.Equal(new[] { "02|A1" }, porAlmacen.Select(f => $"{f["almacen"]}|{f["articulo"]}").ToArray());
            Assert.Equal(new[] { "A2" }, Articulos(porGrupo));
        }

        [Fact]
        public async Task Ejecutar_OrdenPorAlmacenYArticuloYAplicaElLimite()
        {
            var r = Crear(new[] { Cfg("B", "02", 10m, 0m), Cfg("A", "02", 10m, 0m), Cfg("B", "01", 10m, 0m) });

            var filas = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(new[] { "01|B", "02|A" }, filas.Select(f => $"{f["almacen"]}|{f["articulo"]}").ToArray());
        }

        [Fact]
        public async Task Ejecutar_ElLimiteSeAplicaDespuesDelFiltroDeEstado()
        {
            var r = Crear(
                new[] { Cfg("A", "01", 10m, 100m), Cfg("B", "01", 10m, 100m), Cfg("C", "01", 10m, 100m) },
                Ex("A", "01", 50m), Ex("B", "01", 1m), Ex("C", "01", 2m));

            var filas = await r.EjecutarAsync(Filtros(), 1);

            Assert.Equal(new[] { "B" }, Articulos(filas));
        }
    }
}
