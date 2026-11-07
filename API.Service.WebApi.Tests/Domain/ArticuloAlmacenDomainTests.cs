using API.Domain.Core;
using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ArticuloAlmacenDomainTests
    {
        private readonly Mock<IRepositorioGenerico<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)>> _repoConfig = new();
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArticulo = new();
        private readonly Mock<IRepositorioGenerico<Almacen, string>> _repoAlmacen = new();
        private readonly Mock<IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>> _repoExistencia = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly ArticuloAlmacenDomain _domain;

        public ArticuloAlmacenDomainTests()
        {
            _domain = new ArticuloAlmacenDomain(_repoConfig.Object, _repoArticulo.Object, _repoAlmacen.Object, _repoExistencia.Object, _tx.Object);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoConfig.Setup(r => r.ActualizarAsync(It.IsAny<(string, string)>(), It.IsAny<ArticuloAlmacen>())).ReturnsAsync(true);
            _repoConfig.Setup(r => r.EliminarAsync(It.IsAny<(string, string)>())).ReturnsAsync(true);
            _repoConfig.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<ArticuloAlmacen>())).Returns(Task.CompletedTask);
            Config();
            Existencias();
            Almacenes(Alm("A1"), Alm("A2"));
        }

        private static Almacen Alm(string cod, string activo = "S") => new() { Codigo = cod, Nombre = "Almacén " + cod, Activo = activo };

        private static Articulo Art(string cod, string activo = "S", string inventario = "S") =>
            new() { Codigo = cod, Nombre = "Artículo " + cod, Activo = activo, ArticuloInventario = inventario };

        private void Config(params ArticuloAlmacen[] filas) =>
            _repoConfig.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());

        private void Existencias(params ExistenciaArticulo[] filas) =>
            _repoExistencia.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());

        private void Almacenes(params Almacen[] filas) =>
            _repoAlmacen.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());

        private void Articulos(params Articulo[] filas)
        {
            _repoArticulo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());
            foreach (var a in filas)
                _repoArticulo.Setup(r => r.ObtenerAsync(a.Codigo)).ReturnsAsync(a);
        }

        private static ExistenciaArticulo Ex(string art, string alm, decimal disponible, decimal comprometido = 0, decimal pedido = 0) =>
            new() { CodArticulo = art, CodAlmacen = alm, Disponible = disponible, Comprometido = comprometido, Pedido = pedido };

        private static ArticuloAlmacen Cfg(string art, string alm, decimal? min, decimal? max, string bloqueado = "N") =>
            new() { CodArticulo = art, CodAlmacen = alm, Minimo = min, Maximo = max, Bloqueado = bloqueado };

        private void NadaTocado()
        {
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>()), Times.Never);
            _repoConfig.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<ArticuloAlmacen>()), Times.Never);
            _repoConfig.Verify(r => r.ActualizarAsync(It.IsAny<(string, string)>(), It.IsAny<ArticuloAlmacen>()), Times.Never);
            _repoConfig.Verify(r => r.EliminarAsync(It.IsAny<(string, string)>()), Times.Never);
        }

        // ---------- ObtenerFilasAsync ----------

        [Fact]
        public async Task ObtenerFilasAsync_ArticuloInexistente_LanzaArticuloNoExiste()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("X")).ReturnsAsync((Articulo?)null);

            var ex = await Assert.ThrowsAsync<ArticuloNoExisteException>(() => _domain.ObtenerFilasAsync("X"));

            Assert.Equal("El artículo X no existe.", ex.Message);
        }

        [Fact]
        public async Task ObtenerFilasAsync_ListaTodosLosAlmacenesActivosAunqueNoHayaExistenciasNiConfiguracion()
        {
            Articulos(Art("ART1"));

            var filas = await _domain.ObtenerFilasAsync("ART1");

            Assert.Equal(new[] { "A1", "A2" }, filas.Select(f => f.CodAlmacen));
            Assert.All(filas, f =>
            {
                Assert.Equal(0m, f.EnStock);
                Assert.Equal(0m, f.Disponible);
                Assert.Equal("OK", f.Estado);
                Assert.False(f.Bloqueado);
                Assert.Null(f.Minimo);
                Assert.Null(f.Maximo);
                Assert.Null(f.NivelNecesario);
            });
            Assert.Equal("Almacén A1", filas[0].NombreAlmacen);
        }

        [Fact]
        public async Task ObtenerFilasAsync_AlmacenInactivoSoloApareceConConfiguracionOExistencias_OrdenadoPorCodigo()
        {
            Articulos(Art("ART1"));
            Almacenes(Alm("A9"), Alm("A1"), Alm("I1", "N"), Alm("I2", "N"), Alm("I3", "N"));
            Config(Cfg("ART1", "I1", 5m, null));
            Existencias(Ex("ART1", "I2", 3m), Ex("OTRO", "I3", 7m));

            var filas = await _domain.ObtenerFilasAsync("ART1");

            Assert.Equal(new[] { "A1", "A9", "I1", "I2" }, filas.Select(f => f.CodAlmacen));
        }

        [Fact]
        public async Task ObtenerFilasAsync_CalculaDisponibleNivelNecesarioYEstadoPorFila()
        {
            Articulos(Art("ART1"));
            Existencias(
                Ex("ART1", "A1", 4m, comprometido: 1m, pedido: 2m),
                Ex("ART1", "A2", 150m),
                Ex("OTRO", "A1", 999m));
            Config(
                Cfg("ART1", "A1", 10m, 100m, "S"),
                Cfg("ART1", "A2", null, 100m),
                Cfg("OTRO", "A1", 1m, 1m));

            var filas = await _domain.ObtenerFilasAsync("ART1");

            var a1 = filas[0];
            Assert.Equal(4m, a1.EnStock);
            Assert.Equal(1m, a1.Comprometido);
            Assert.Equal(2m, a1.Pedido);
            Assert.Equal(5m, a1.Disponible);
            Assert.Equal(10m, a1.Minimo);
            Assert.Equal(100m, a1.Maximo);
            Assert.Equal(95m, a1.NivelNecesario);
            Assert.Equal("BAJO_MINIMO", a1.Estado);
            Assert.True(a1.Bloqueado);

            var a2 = filas[1];
            Assert.Equal(150m, a2.Disponible);
            Assert.Equal(0m, a2.NivelNecesario);
            Assert.Equal("SOBRE_MAXIMO", a2.Estado);
            Assert.False(a2.Bloqueado);
        }

        [Fact]
        public async Task ObtenerFilasAsync_UnaConsultaPorTabla()
        {
            Articulos(Art("ART1"));

            await _domain.ObtenerFilasAsync("ART1");

            _repoConfig.Verify(r => r.ObtenerTodoAsync(), Times.Once);
            _repoExistencia.Verify(r => r.ObtenerTodoAsync(), Times.Once);
            _repoAlmacen.Verify(r => r.ObtenerTodoAsync(), Times.Once);
        }

        // ---------- GuardarAsync: escritura ----------

        [Fact]
        public async Task GuardarAsync_CreaLasFilasNuevasConAgregarSinGuardar()
        {
            Articulos(Art("ART1"));

            await _domain.GuardarAsync("ART1", new[]
            {
                new ArticuloAlmacenConfig("A1", 5m, 50m, false),
                new ArticuloAlmacenConfig("A2", null, null, true),
            });

            _repoConfig.Verify(r => r.AgregarSinGuardarAsync(It.Is<ArticuloAlmacen>(
                x => x.CodArticulo == "ART1" && x.CodAlmacen == "A1" && x.Minimo == 5m && x.Maximo == 50m && x.Bloqueado == "N")), Times.Once);
            _repoConfig.Verify(r => r.AgregarSinGuardarAsync(It.Is<ArticuloAlmacen>(
                x => x.CodArticulo == "ART1" && x.CodAlmacen == "A2" && x.Minimo == null && x.Maximo == null && x.Bloqueado == "S")), Times.Once);
            _repoConfig.Verify(r => r.ActualizarAsync(It.IsAny<(string, string)>(), It.IsAny<ArticuloAlmacen>()), Times.Never);
            _repoConfig.Verify(r => r.EliminarAsync(It.IsAny<(string, string)>()), Times.Never);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GuardarAsync_ActualizaLasExistentesYEliminaLasQueYaNoVienen()
        {
            Articulos(Art("ART1"));
            Config(
                Cfg("ART1", "A1", 1m, 2m),
                Cfg("ART1", "A2", 3m, 4m),
                Cfg("OTRO", "A1", 9m, 9m));

            await _domain.GuardarAsync("ART1", new[] { new ArticuloAlmacenConfig("A1", 10m, 20m, true) });

            _repoConfig.Verify(r => r.ActualizarAsync(
                It.Is<(string, string)>(k => k.Item1 == "ART1" && k.Item2 == "A1"),
                It.Is<ArticuloAlmacen>(x => x.Minimo == 10m && x.Maximo == 20m && x.Bloqueado == "S")), Times.Once);
            _repoConfig.Verify(r => r.EliminarAsync(It.Is<(string, string)>(k => k.Item1 == "ART1" && k.Item2 == "A2")), Times.Once);
            _repoConfig.Verify(r => r.EliminarAsync(It.Is<(string, string)>(k => k.Item1 == "OTRO")), Times.Never);
            _repoConfig.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<ArticuloAlmacen>()), Times.Never);
        }

        [Fact]
        public async Task GuardarAsync_FilaExistenteQueQuedaVacia_SeElimina()
        {
            Articulos(Art("ART1"));
            Config(Cfg("ART1", "A1", 1m, 2m, "S"));

            await _domain.GuardarAsync("ART1", new[] { new ArticuloAlmacenConfig("A1", 0m, 0m, false) });

            _repoConfig.Verify(r => r.EliminarAsync(It.Is<(string, string)>(k => k.Item1 == "ART1" && k.Item2 == "A1")), Times.Once);
            _repoConfig.Verify(r => r.ActualizarAsync(It.IsAny<(string, string)>(), It.IsAny<ArticuloAlmacen>()), Times.Never);
            _repoConfig.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<ArticuloAlmacen>()), Times.Never);
        }

        [Fact]
        public async Task GuardarAsync_FilaConMinimoCeroMaximoCeroYSinBloqueo_NoSeGuarda()
        {
            Articulos(Art("ART1"));

            await _domain.GuardarAsync("ART1", new[] { new ArticuloAlmacenConfig("A1", 0m, 0m, false) });

            _repoConfig.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<ArticuloAlmacen>()), Times.Never);
            _repoConfig.Verify(r => r.ActualizarAsync(It.IsAny<(string, string)>(), It.IsAny<ArticuloAlmacen>()), Times.Never);
        }

        [Fact]
        public async Task GuardarAsync_MinimoCeroMaximoCeroConBloqueo_SeGuardaComoNullNullS()
        {
            Articulos(Art("ART1"));

            await _domain.GuardarAsync("ART1", new[] { new ArticuloAlmacenConfig("A1", 0m, 0m, true) });

            _repoConfig.Verify(r => r.AgregarSinGuardarAsync(It.Is<ArticuloAlmacen>(
                x => x.CodAlmacen == "A1" && x.Minimo == null && x.Maximo == null && x.Bloqueado == "S")), Times.Once);
        }

        [Fact]
        public async Task GuardarAsync_ListaVacia_EliminaTodaLaConfiguracionDelArticulo()
        {
            Articulos(Art("ART1"));
            Config(Cfg("ART1", "A1", 1m, null), Cfg("ART1", "A2", null, 5m));

            await _domain.GuardarAsync("ART1", new List<ArticuloAlmacenConfig>());

            _repoConfig.Verify(r => r.EliminarAsync(It.IsAny<(string, string)>()), Times.Exactly(2));
            _repoConfig.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<ArticuloAlmacen>()), Times.Never);
        }

        [Fact]
        public async Task GuardarAsync_AdmiteSeisDecimalesYMaximoIgualAlMinimo()
        {
            Articulos(Art("ART1"));

            await _domain.GuardarAsync("ART1", new[] { new ArticuloAlmacenConfig("A1", 1.123456m, 1.123456m, false) });

            _repoConfig.Verify(r => r.AgregarSinGuardarAsync(It.Is<ArticuloAlmacen>(x => x.Minimo == 1.123456m && x.Maximo == 1.123456m)), Times.Once);
        }

        // ---------- GuardarAsync: validaciones ----------

        [Fact]
        public async Task GuardarAsync_ArticuloInexistente_Lanza()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("X")).ReturnsAsync((Articulo?)null);

            var ex = await Assert.ThrowsAsync<ArticuloNoExisteException>(
                () => _domain.GuardarAsync("X", new[] { new ArticuloAlmacenConfig("A1", 1m, null, false) }));

            Assert.Equal("El artículo X no existe.", ex.Message);
            NadaTocado();
        }

        [Fact]
        public async Task GuardarAsync_AlmacenInexistente_LanzaYNoTocaNada()
        {
            Articulos(Art("ART1"));

            var ex = await Assert.ThrowsAsync<AlmacenNoExisteException>(() => _domain.GuardarAsync("ART1", new[]
            {
                new ArticuloAlmacenConfig("A1", 1m, null, false),
                new ArticuloAlmacenConfig("ZZ", 1m, null, false),
            }));

            Assert.Equal("El almacén ZZ no existe.", ex.Message);
            NadaTocado();
        }

        [Fact]
        public async Task GuardarAsync_AlmacenRepetido_LanzaYNoTocaNada()
        {
            Articulos(Art("ART1"));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.GuardarAsync("ART1", new[]
            {
                new ArticuloAlmacenConfig("A1", 1m, null, false),
                new ArticuloAlmacenConfig("A1", 2m, null, false),
            }));

            Assert.Equal("El almacén A1 está repetido en la lista.", ex.Message);
            NadaTocado();
        }

        [Fact]
        public async Task GuardarAsync_MinimoNegativo_LanzaYNoTocaNada()
        {
            Articulos(Art("ART1"));

            var ex = await Assert.ThrowsAsync<Exception>(
                () => _domain.GuardarAsync("ART1", new[] { new ArticuloAlmacenConfig("A1", -1m, null, false) }));

            Assert.Equal("El stock mínimo del almacén A1 no puede ser negativo.", ex.Message);
            NadaTocado();
        }

        [Fact]
        public async Task GuardarAsync_MaximoNegativo_LanzaYNoTocaNada()
        {
            Articulos(Art("ART1"));

            var ex = await Assert.ThrowsAsync<Exception>(
                () => _domain.GuardarAsync("ART1", new[] { new ArticuloAlmacenConfig("A1", null, -0.5m, false) }));

            Assert.Equal("El stock máximo del almacén A1 no puede ser negativo.", ex.Message);
            NadaTocado();
        }

        [Fact]
        public async Task GuardarAsync_MinimoConMasDeSeisDecimales_LanzaYNoTocaNada()
        {
            Articulos(Art("ART1"));

            var ex = await Assert.ThrowsAsync<Exception>(
                () => _domain.GuardarAsync("ART1", new[] { new ArticuloAlmacenConfig("A1", 1.1234567m, null, false) }));

            Assert.Equal("El stock mínimo del almacén A1 admite como máximo 6 decimales.", ex.Message);
            NadaTocado();
        }

        [Fact]
        public async Task GuardarAsync_MaximoConMasDeSeisDecimales_LanzaYNoTocaNada()
        {
            Articulos(Art("ART1"));

            var ex = await Assert.ThrowsAsync<Exception>(
                () => _domain.GuardarAsync("ART1", new[] { new ArticuloAlmacenConfig("A1", null, 2.0000001m, false) }));

            Assert.Equal("El stock máximo del almacén A1 admite como máximo 6 decimales.", ex.Message);
            NadaTocado();
        }

        // Límite de decimal(19,6): 13 enteros y 6 decimales.
        private const decimal LimiteSuperior = 9999999999999.999999m;

        [Fact]
        public async Task GuardarAsync_MinimoYMaximoExactamenteEnElLimite_Pasan()
        {
            Articulos(Art("ART1"));

            await _domain.GuardarAsync("ART1", new[]
            {
                new ArticuloAlmacenConfig("A1", LimiteSuperior, LimiteSuperior, false),
            });

            _repoConfig.Verify(r => r.AgregarSinGuardarAsync(It.Is<ArticuloAlmacen>(x => x.Minimo == LimiteSuperior && x.Maximo == LimiteSuperior)), Times.Once);
        }

        [Theory]
        [InlineData("10000000000000")]
        [InlineData("9999999999999.9999995")]
        [InlineData("79228162514264337593543950335")]
        public async Task GuardarAsync_MinimoSobreElLimite_LanzaYNoTocaNada(string valor)
        {
            Articulos(Art("ART1"));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.GuardarAsync("ART1", new[]
            {
                new ArticuloAlmacenConfig("A1", decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture), null, false),
            }));

            Assert.Equal("El stock mínimo del almacén A1 no puede superar 9,999,999,999,999.999999.", ex.Message);
            NadaTocado();
        }

        [Theory]
        [InlineData("10000000000000")]
        [InlineData("9999999999999.9999995")]
        [InlineData("79228162514264337593543950335")]
        public async Task GuardarAsync_MaximoSobreElLimite_LanzaYNoTocaNada(string valor)
        {
            Articulos(Art("ART1"));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.GuardarAsync("ART1", new[]
            {
                new ArticuloAlmacenConfig("A1", null, decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture), false),
            }));

            Assert.Equal("El stock máximo del almacén A1 no puede superar 9,999,999,999,999.999999.", ex.Message);
            NadaTocado();
        }

        [Fact]
        public async Task GuardarAsync_SobreElLimiteEnUnaFilaConConfiguracionExistente_NoModificaNiElimina()
        {
            Articulos(Art("ART1"));
            Config(Cfg("ART1", "A1", 1m, 2m), Cfg("ART1", "A2", 3m, 4m));

            await Assert.ThrowsAsync<Exception>(() => _domain.GuardarAsync("ART1", new[]
            {
                new ArticuloAlmacenConfig("A1", 5m, 6m, false),
                new ArticuloAlmacenConfig("A2", 1m, 10000000000000m, false),
            }));

            NadaTocado();
        }

        [Fact]
        public async Task GuardarAsync_MaximoMenorQueMinimo_LanzaYNoTocaNada()
        {
            Articulos(Art("ART1"));

            var ex = await Assert.ThrowsAsync<Exception>(
                () => _domain.GuardarAsync("ART1", new[] { new ArticuloAlmacenConfig("A1", 10m, 9.999999m, false) }));

            Assert.Equal("El stock máximo del almacén A1 no puede ser menor que el mínimo.", ex.Message);
            NadaTocado();
        }

        [Fact]
        public async Task GuardarAsync_ErrorEnUnaFilaConConfiguracionExistente_NoModificaNiElimina()
        {
            Articulos(Art("ART1"));
            Config(Cfg("ART1", "A1", 1m, 2m), Cfg("ART1", "A2", 3m, 4m));

            await Assert.ThrowsAsync<Exception>(() => _domain.GuardarAsync("ART1", new[]
            {
                new ArticuloAlmacenConfig("A1", 5m, 6m, false),
                new ArticuloAlmacenConfig("A2", -1m, null, false),
            }));

            NadaTocado();
        }

        // ---------- ObtenerBajoMinimoAsync ----------

        [Fact]
        public async Task ObtenerBajoMinimoAsync_SoloInventarioActivoConMinimoMayorQueCeroBajoStock()
        {
            Articulos(
                Art("ART1"),
                Art("ART2", activo: "N"),
                Art("ART3", inventario: "N"),
                Art("ART4"),
                Art("ART5"),
                Art("ART6"));
            Almacenes(Alm("A1"), Alm("A2"), Alm("I1", "N"));
            Config(
                Cfg("ART1", "A1", 10m, null),        // 4 < 10: aparece
                Cfg("ART1", "A2", 10m, null),        // 10 no es < 10: no
                Cfg("ART2", "A1", 10m, null),        // articulo inactivo: no
                Cfg("ART3", "A1", 10m, null),        // no inventario: no
                Cfg("ART4", "A1", 5m, null, "S"),    // sin existencia = 0 < 5 y bloqueado: aparece igual
                Cfg("ART5", "A1", null, 100m),       // sin minimo: no
                Cfg("ART6", "I1", 10m, null));       // almacen inactivo: no
            Existencias(
                Ex("ART1", "A1", 4m),
                Ex("ART1", "A2", 10m),
                Ex("ART2", "A1", 0m),
                Ex("ART3", "A1", 0m),
                Ex("ART5", "A1", 0m),
                Ex("ART6", "I1", 0m));

            var lista = await _domain.ObtenerBajoMinimoAsync();

            Assert.Equal(2, lista.Count);
            var a1 = lista[0];
            Assert.Equal(("ART1", "Artículo ART1", "A1", "Almacén A1", 4m, 10m),
                (a1.CodArticulo, a1.NombreArticulo, a1.CodAlmacen, a1.NombreAlmacen, a1.EnStock, a1.Minimo));
            var a4 = lista[1];
            Assert.Equal(("ART4", "A1", 0m, 5m), (a4.CodArticulo, a4.CodAlmacen, a4.EnStock, a4.Minimo));
        }

        [Fact]
        public async Task ObtenerBajoMinimoAsync_MinimoCeroGuardadoAMano_NoAvisa()
        {
            Articulos(Art("ART1"));
            Config(Cfg("ART1", "A1", 0m, null));

            var lista = await _domain.ObtenerBajoMinimoAsync();

            Assert.Empty(lista);
        }

        [Fact]
        public async Task ObtenerBajoMinimoAsync_SinConfiguracion_DevuelveVacio()
        {
            var lista = await _domain.ObtenerBajoMinimoAsync();

            Assert.Empty(lista);
        }

        // ---------- ObtenerConfiguracionAsync ----------

        [Fact]
        public async Task ObtenerConfiguracionAsync_SinFiltros_DevuelveTodas()
        {
            Config(Cfg("ART1", "A1", 1m, 2m, "S"), Cfg("ART2", "A2", null, 5m));

            var dic = await _domain.ObtenerConfiguracionAsync(null, null);

            Assert.Equal(2, dic.Count);
            Assert.Equal(new ArticuloAlmacenConfig("A1", 1m, 2m, true), dic[("ART1", "A1")]);
            Assert.Equal(new ArticuloAlmacenConfig("A2", null, 5m, false), dic[("ART2", "A2")]);
        }

        [Fact]
        public async Task ObtenerConfiguracionAsync_FiltraPorArticuloYAlmacen()
        {
            Config(Cfg("ART1", "A1", 1m, null), Cfg("ART1", "A2", 2m, null), Cfg("ART2", "A1", 3m, null));

            var porArticulo = await _domain.ObtenerConfiguracionAsync("ART1", null);
            var porAlmacen = await _domain.ObtenerConfiguracionAsync(null, "A1");
            var porAmbos = await _domain.ObtenerConfiguracionAsync("ART2", "A1");
            var vacio = await _domain.ObtenerConfiguracionAsync(" ", "");

            Assert.Equal(2, porArticulo.Count);
            Assert.Equal(2, porAlmacen.Count);
            var unica = Assert.Single(porAmbos);
            Assert.Equal(3m, unica.Value.Minimo);
            Assert.Equal(3, vacio.Count);
        }
    }
}
