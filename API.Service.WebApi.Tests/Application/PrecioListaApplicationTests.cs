using API.Application.Main;
using API.Domain.Core.Inventario;
using API.Domain.Core.Precio;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class PrecioListaApplicationTests
    {
        private readonly Mock<IPrecioListaDomain> _domain = new();
        private readonly PrecioListaApplication _app;

        public PrecioListaApplicationTests()
        {
            _app = new PrecioListaApplication(_domain.Object);
        }

        [Fact]
        public async Task ResolverAsync_Ok_MapeaLos4Campos()
        {
            _domain.Setup(d => d.ResolverAsync("0003-0002", 1))
                .ReturnsAsync(new PrecioResuelto("0003-0002", 22m, 1.20m, 26.40m));

            var r = await _app.ResolverAsync("0003-0002", 1);

            Assert.True(r.Resultado);
            Assert.Equal("0003-0002", r.Dato!.Codigo);
            Assert.Equal(22m, r.Dato!.PrecioBase);
            Assert.Equal(1.20m, r.Dato!.FactorEfectivo);
            Assert.Equal(26.40m, r.Dato!.PrecioLista);
        }

        [Fact]
        public async Task ResolverAsync_ArticuloNoExiste_MensajeContieneElArticulo()
        {
            _domain.Setup(d => d.ResolverAsync("NOPE", null))
                .ThrowsAsync(new ArticuloNoExisteException("NOPE"));

            var r = await _app.ResolverAsync("NOPE", null);

            Assert.False(r.Resultado);
            Assert.True(r.Mensaje.Contains("El artículo", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolverAsync_ListaInvalida_MensajeNoContieneElArticulo()
        {
            _domain.Setup(d => d.ResolverAsync("0003-0002", 999))
                .ThrowsAsync(PrecioListaInvalidaException.Circular());

            var r = await _app.ResolverAsync("0003-0002", 999);

            Assert.False(r.Resultado);
            Assert.False(r.Mensaje.Contains("El artículo", StringComparison.OrdinalIgnoreCase));
        }
    }
}
