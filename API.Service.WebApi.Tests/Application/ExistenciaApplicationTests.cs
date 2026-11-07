using API.Application.DTO.inventario;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Transversal.Mapper;
using AutoMapper;
using AutoMapper.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class ExistenciaApplicationTests
    {
        private readonly Mock<IExistenciaDomain> _domain = new();
        private readonly Mock<IArticuloAlmacenDomain> _config = new();
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly ExistenciaApplication _app;

        public ExistenciaApplicationTests()
        {
            _app = new ExistenciaApplication(_domain.Object, _mapper, _config.Object);
        }

        private static ExistenciaArticulo Existencia(string art, string alm, decimal disponible) =>
            new() { CodArticulo = art, CodAlmacen = alm, Disponible = disponible };

        private static IReadOnlyDictionary<(string CodArticulo, string CodAlmacen), ArticuloAlmacenConfig> Config(
            params (string Art, ArticuloAlmacenConfig Cfg)[] filas) =>
            filas.ToDictionary(f => (f.Art, f.Cfg.CodAlmacen), f => f.Cfg);

        [Fact]
        public void ElMapaDeExistenciaNoDejaMiembrosSinMapearNiSinIgnorar()
        {
            // AssertConfigurationIsValid() global ya falla por otros mapas ajenos a esta fase: se valida solo este.
            var config = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance);

            var mapa = config.Internal().FindTypeMapFor<ExistenciaArticulo, ExistenciaArticuloDTO>();

            Assert.NotNull(mapa);
            Assert.Empty(mapa!.GetUnmappedPropertyNames());
        }

        [Fact]
        public void ElMapaDeExistenciaDejaLosCamposDeConfiguracionPorDefecto()
        {
            var dto = _mapper.Map<ExistenciaArticuloDTO>(Existencia("A1", "01", 4m));

            Assert.Equal(4m, dto.Disponible);
            Assert.Null(dto.Minimo);
            Assert.Null(dto.Maximo);
            Assert.False(dto.Bloqueado);
            Assert.Equal("OK", dto.Estado);
        }

        [Fact]
        public async Task ObtenerTodoAsync_LlenaMinimoMaximoBloqueadoYEstadoDeCadaFila()
        {
            _domain.Setup(d => d.ObtenerTodoAsync(null, null)).ReturnsAsync(new[]
            {
                Existencia("A1", "01", 2m),   // bajo mínimo
                Existencia("A1", "02", 50m),  // sobre máximo
                Existencia("A2", "01", 10m),  // dentro de niveles
                Existencia("A3", "01", 7m),   // sin configuración
            });
            _config.Setup(c => c.ObtenerConfiguracionAsync(null, null)).ReturnsAsync(Config(
                ("A1", new ArticuloAlmacenConfig("01", 5m, 20m, true)),
                ("A1", new ArticuloAlmacenConfig("02", 5m, 20m, false)),
                ("A2", new ArticuloAlmacenConfig("01", 5m, 20m, false))));

            var r = await _app.ObtenerTodoAsync(null, null);

            Assert.True(r.Resultado);
            var filas = r.Dato.ToList();
            Assert.Equal(("BAJO_MINIMO", (decimal?)5m, (decimal?)20m, true), (filas[0].Estado, filas[0].Minimo, filas[0].Maximo, filas[0].Bloqueado));
            Assert.Equal(("SOBRE_MAXIMO", (decimal?)5m, (decimal?)20m, false), (filas[1].Estado, filas[1].Minimo, filas[1].Maximo, filas[1].Bloqueado));
            Assert.Equal(("OK", (decimal?)5m, (decimal?)20m, false), (filas[2].Estado, filas[2].Minimo, filas[2].Maximo, filas[2].Bloqueado));
            Assert.Equal(("OK", (decimal?)null, (decimal?)null, false), (filas[3].Estado, filas[3].Minimo, filas[3].Maximo, filas[3].Bloqueado));
        }

        [Fact]
        public async Task ObtenerTodoAsync_SinConfiguracion_DejaNulosFalsoYOk()
        {
            _domain.Setup(d => d.ObtenerTodoAsync(null, null)).ReturnsAsync(new[] { Existencia("A3", "01", 7m) });
            _config.Setup(c => c.ObtenerConfiguracionAsync(null, null)).ReturnsAsync(Config());

            var r = await _app.ObtenerTodoAsync(null, null);

            var fila = Assert.Single(r.Dato);
            Assert.Null(fila.Minimo);
            Assert.Null(fila.Maximo);
            Assert.False(fila.Bloqueado);
            Assert.Equal("OK", fila.Estado);
            Assert.Equal(7m, fila.Disponible);
        }

        [Fact]
        public async Task ObtenerTodoAsync_UnaSolaLlamadaAlDominioDeConfiguracionConLosFiltros()
        {
            _domain.Setup(d => d.ObtenerTodoAsync("A1", "01")).ReturnsAsync(new[]
            {
                Existencia("A1", "01", 1m), Existencia("A1", "01", 2m), Existencia("A1", "01", 3m),
            });
            _config.Setup(c => c.ObtenerConfiguracionAsync("A1", "01")).ReturnsAsync(Config());

            await _app.ObtenerTodoAsync("A1", "01");

            _config.Verify(c => c.ObtenerConfiguracionAsync(It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
            _config.Verify(c => c.ObtenerConfiguracionAsync("A1", "01"), Times.Once);
        }

        [Fact]
        public async Task ObtenerAsync_ConFila_LlenaLaConfiguracionConUnaSolaLlamada()
        {
            _domain.Setup(d => d.ObtenerAsync("A1", "01")).ReturnsAsync(Existencia("A1", "01", 2m));
            _config.Setup(c => c.ObtenerConfiguracionAsync("A1", "01")).ReturnsAsync(Config(
                ("A1", new ArticuloAlmacenConfig("01", 5m, null, true))));

            var r = await _app.ObtenerAsync("A1", "01");

            Assert.True(r.Resultado);
            Assert.Equal(5m, r.Dato.Minimo);
            Assert.Null(r.Dato.Maximo);
            Assert.True(r.Dato.Bloqueado);
            Assert.Equal("BAJO_MINIMO", r.Dato.Estado);
            _config.Verify(c => c.ObtenerConfiguracionAsync(It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task ObtenerAsync_SinFilaSinConfiguracion_DevuelveElDtoPorDefectoConEstadoOk()
        {
            _domain.Setup(d => d.ObtenerAsync("A1", "01")).ReturnsAsync((ExistenciaArticulo?)null);
            _config.Setup(c => c.ObtenerConfiguracionAsync("A1", "01")).ReturnsAsync(Config());

            var r = await _app.ObtenerAsync("A1", "01");

            Assert.True(r.Resultado);
            Assert.Equal("A1", r.Dato.CodArticulo);
            Assert.Equal("01", r.Dato.CodAlmacen);
            Assert.Equal(0m, r.Dato.Disponible);
            Assert.Null(r.Dato.Minimo);
            Assert.False(r.Dato.Bloqueado);
            Assert.Equal("OK", r.Dato.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_SinFilaPeroConConfiguracion_LlenaLaConfiguracion()
        {
            _domain.Setup(d => d.ObtenerAsync("A1", "01")).ReturnsAsync((ExistenciaArticulo?)null);
            _config.Setup(c => c.ObtenerConfiguracionAsync("A1", "01")).ReturnsAsync(Config(
                ("A1", new ArticuloAlmacenConfig("01", 5m, 20m, true))));

            var r = await _app.ObtenerAsync("A1", "01");

            Assert.Equal(0m, r.Dato.Disponible);
            Assert.Equal(5m, r.Dato.Minimo);
            Assert.Equal(20m, r.Dato.Maximo);
            Assert.True(r.Dato.Bloqueado);
            Assert.Equal("BAJO_MINIMO", r.Dato.Estado);
        }

        [Fact]
        public async Task ObtenerPorArticuloAsync_FiltraLaConfiguracionPorArticuloYLlenaCadaFila()
        {
            _domain.Setup(d => d.ObtenerPorArticuloAsync("A1")).ReturnsAsync(new[]
            {
                Existencia("A1", "01", 30m), Existencia("A1", "02", 8m),
            });
            _config.Setup(c => c.ObtenerConfiguracionAsync("A1", null)).ReturnsAsync(Config(
                ("A1", new ArticuloAlmacenConfig("01", 5m, 20m, false))));

            var r = await _app.ObtenerPorArticuloAsync("A1");

            var filas = r.Dato.ToList();
            Assert.Equal("SOBRE_MAXIMO", filas[0].Estado);
            Assert.Equal("OK", filas[1].Estado);
            Assert.Null(filas[1].Minimo);
            _config.Verify(c => c.ObtenerConfiguracionAsync(It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task ErrorDelDominioDeConfiguracion_SeCapturaComoResultadoFalso()
        {
            _domain.Setup(d => d.ObtenerTodoAsync(null, null)).ReturnsAsync(new[] { Existencia("A1", "01", 1m) });
            _config.Setup(c => c.ObtenerConfiguracionAsync(null, null)).ThrowsAsync(new Exception("falla"));

            var r = await _app.ObtenerTodoAsync(null, null);

            Assert.False(r.Resultado);
            Assert.Equal("falla", r.Mensaje);
        }
    }
}
