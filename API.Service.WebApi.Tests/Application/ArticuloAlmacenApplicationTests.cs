using API.Application.DTO.articulo.almacen;
using API.Application.Main;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class ArticuloAlmacenApplicationTests
    {
        private readonly Mock<IArticuloAlmacenDomain> _domain = new();
        private readonly ArticuloAlmacenApplication _app;

        public ArticuloAlmacenApplicationTests()
        {
            _app = new ArticuloAlmacenApplication(_domain.Object);
        }

        [Fact]
        public async Task ObtenerAsync_ProyectaCadaFilaAlDtoSinPerderCampos()
        {
            _domain.Setup(d => d.ObtenerFilasAsync("ART1")).ReturnsAsync(new[]
            {
                new ArticuloAlmacenFila("01", "Principal", true, 10m, 2m, 3m, 11m, 5m, 20m, 9m, "OK"),
                new ArticuloAlmacenFila("02", "Sucursal", false, 1m, 0m, 0m, 1m, null, null, null, "BAJO_MINIMO"),
            });

            var r = await _app.ObtenerAsync("ART1");

            Assert.True(r.Resultado);
            var filas = r.Dato.ToList();
            Assert.Equal(2, filas.Count);
            Assert.Equal("01", filas[0].CodAlmacen);
            Assert.Equal("Principal", filas[0].NombreAlmacen);
            Assert.True(filas[0].Bloqueado);
            Assert.Equal(10m, filas[0].EnStock);
            Assert.Equal(2m, filas[0].Comprometido);
            Assert.Equal(3m, filas[0].Pedido);
            Assert.Equal(11m, filas[0].Disponible);
            Assert.Equal(5m, filas[0].Minimo);
            Assert.Equal(20m, filas[0].Maximo);
            Assert.Equal(9m, filas[0].NivelNecesario);
            Assert.Equal("OK", filas[0].Estado);
            Assert.False(filas[1].Bloqueado);
            Assert.Null(filas[1].Minimo);
            Assert.Null(filas[1].Maximo);
            Assert.Null(filas[1].NivelNecesario);
            Assert.Equal("BAJO_MINIMO", filas[1].Estado);
        }

        [Fact]
        public async Task ObtenerAsync_ArticuloInexistente_DevuelveResultadoFalsoConElMensaje()
        {
            _domain.Setup(d => d.ObtenerFilasAsync("X")).ThrowsAsync(new Exception("El artículo X no existe."));

            var r = await _app.ObtenerAsync("X");

            Assert.False(r.Resultado);
            Assert.Equal("El artículo X no existe.", r.Mensaje);
        }

        [Fact]
        public async Task GuardarAsync_ProyectaLasFilasAlDominioYRecortaElCodigoDeAlmacen()
        {
            IReadOnlyList<ArticuloAlmacenConfig>? recibido = null;
            _domain.Setup(d => d.GuardarAsync("ART1", It.IsAny<IReadOnlyList<ArticuloAlmacenConfig>>()))
                .Callback<string, IReadOnlyList<ArticuloAlmacenConfig>>((_, f) => recibido = f)
                .Returns(Task.CompletedTask);

            var r = await _app.GuardarAsync("ART1", new[]
            {
                new ArticuloAlmacenGuardarDTO { CodAlmacen = " 01 ", Minimo = 5m, Maximo = 20m, Bloqueado = true },
                new ArticuloAlmacenGuardarDTO { CodAlmacen = "02", Minimo = null, Maximo = 3m, Bloqueado = false },
            });

            Assert.True(r.Resultado);
            Assert.True(r.Dato);
            Assert.NotNull(recibido);
            Assert.Equal(new ArticuloAlmacenConfig("01", 5m, 20m, true), recibido![0]);
            Assert.Equal(new ArticuloAlmacenConfig("02", null, 3m, false), recibido[1]);
        }

        [Fact]
        public async Task GuardarAsync_Nulo_LlamaAlDominioConListaVacia()
        {
            var llamadas = 0;
            _domain.Setup(d => d.GuardarAsync("ART1", It.Is<IReadOnlyList<ArticuloAlmacenConfig>>(f => f.Count == 0)))
                .Callback(() => llamadas++).Returns(Task.CompletedTask);

            var r = await _app.GuardarAsync("ART1", null);

            Assert.True(r.Resultado);
            Assert.Equal(1, llamadas);
        }

        [Fact]
        public async Task GuardarAsync_ListaVacia_LlamaAlDominioConListaVacia()
        {
            var llamadas = 0;
            _domain.Setup(d => d.GuardarAsync("ART1", It.Is<IReadOnlyList<ArticuloAlmacenConfig>>(f => f.Count == 0)))
                .Callback(() => llamadas++).Returns(Task.CompletedTask);

            var r = await _app.GuardarAsync("ART1", Array.Empty<ArticuloAlmacenGuardarDTO>());

            Assert.True(r.Resultado);
            Assert.Equal(1, llamadas);
        }

        [Fact]
        public async Task GuardarAsync_ErrorDeValidacionDelDominio_DevuelveResultadoFalsoConElMensaje()
        {
            _domain.Setup(d => d.GuardarAsync("ART1", It.IsAny<IReadOnlyList<ArticuloAlmacenConfig>>()))
                .ThrowsAsync(new Exception("El almacén 01 está repetido en la lista."));

            var r = await _app.GuardarAsync("ART1", new[] { new ArticuloAlmacenGuardarDTO { CodAlmacen = "01" } });

            Assert.False(r.Resultado);
            Assert.Equal("El almacén 01 está repetido en la lista.", r.Mensaje);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GuardarAsync_CodigoDeAlmacenVacio_EsErrorDeValidacionYNoLlegaAlDominio(string? codAlmacen)
        {
            var r = await _app.GuardarAsync("ART1", new[]
            {
                new ArticuloAlmacenGuardarDTO { CodAlmacen = "01", Minimo = 1m },
                new ArticuloAlmacenGuardarDTO { CodAlmacen = codAlmacen! },
            });

            Assert.False(r.Resultado);
            Assert.Equal(ArticuloAlmacenApplication.AlmacenSinCodigo, r.Mensaje);
            _domain.Verify(d => d.GuardarAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ArticuloAlmacenConfig>>()), Times.Never);
        }

        [Fact]
        public async Task GuardarAsync_FilaNula_EsErrorDeValidacion()
        {
            var r = await _app.GuardarAsync("ART1", new ArticuloAlmacenGuardarDTO[] { null! });

            Assert.False(r.Resultado);
            Assert.Equal(ArticuloAlmacenApplication.AlmacenSinCodigo, r.Mensaje);
            _domain.Verify(d => d.GuardarAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ArticuloAlmacenConfig>>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerBajoMinimoAsync_ProyectaAlDto()
        {
            _domain.Setup(d => d.ObtenerBajoMinimoAsync()).ReturnsAsync(new[]
            {
                new AlmacenBajoMinimo("ART1", "Aceite", "01", "Principal", 2m, 5m),
            });

            var r = await _app.ObtenerBajoMinimoAsync();

            Assert.True(r.Resultado);
            var fila = Assert.Single(r.Dato);
            Assert.Equal("ART1", fila.CodArticulo);
            Assert.Equal("Aceite", fila.NombreArticulo);
            Assert.Equal("01", fila.CodAlmacen);
            Assert.Equal("Principal", fila.NombreAlmacen);
            Assert.Equal(2m, fila.EnStock);
            Assert.Equal(5m, fila.Minimo);
        }

        [Fact]
        public async Task ObtenerBajoMinimoAsync_CapturaLaExcepcion()
        {
            _domain.Setup(d => d.ObtenerBajoMinimoAsync()).ThrowsAsync(new Exception("falla"));

            var r = await _app.ObtenerBajoMinimoAsync();

            Assert.False(r.Resultado);
            Assert.Equal("falla", r.Mensaje);
        }
    }
}
