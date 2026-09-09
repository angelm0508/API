using API.Domain.Core.Inventario;
using API.Domain.Core.Precio;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PrecioListaDomainTests
    {
        private readonly Mock<IArticuloDomain> _articulos = new();
        private readonly Mock<IListadoPrecioDomain> _listas = new();
        private readonly PrecioListaDomain _sut;

        public PrecioListaDomainTests()
        {
            _sut = new PrecioListaDomain(_articulos.Object, _listas.Object);
        }

        private void Articulo(string cod, decimal? precio) =>
            _articulos.Setup(a => a.ObtenerPorCodigoAsync(cod))
                .ReturnsAsync(new Articulo { Codigo = cod, PrecioUnitario = precio });

        private void Lista(int entry, decimal? factor, int? bas = null, short? metodo = null, string? regla = null) =>
            _listas.Setup(l => l.ObtenerAsync(entry))
                .ReturnsAsync(new ListadoPrecio { Entry = entry, Factor = factor, Base = bas, MetodoRedondeo = metodo, ReglaRedondeo = regla });

        [Fact]
        public async Task SinLista_DevuelvePrecioBaseRedondeado()
        {
            Articulo("A", 22m);
            var r = await _sut.ResolverAsync("A", null);
            Assert.Equal(22.00m, r.PrecioLista);
            Assert.Equal(1m, r.FactorEfectivo);
            Assert.Equal(22m, r.PrecioBase);
        }

        [Fact]
        public async Task ListaConFactor_MultiplicaYRedondea()
        {
            Articulo("0003-0002", 22m);
            Lista(1, 1.20m);
            var r = await _sut.ResolverAsync("0003-0002", 1);
            Assert.Equal(26.40m, r.PrecioLista);
            Assert.Equal(1.20m, r.FactorEfectivo);
        }

        [Fact]
        public async Task CadenaBase_MultiplicaLosFactores()
        {
            Articulo("A", 100m);
            Lista(10, 1.10m, bas: 20);  // asignada
            Lista(20, 1.20m, bas: null); // raíz
            var r = await _sut.ResolverAsync("A", 10);
            Assert.Equal(1.32m, r.FactorEfectivo);
            Assert.Equal(132.00m, r.PrecioLista);
        }

        [Fact]
        public async Task Redondeo_UsaConfigDeLaListaAsignada()
        {
            Articulo("0003-0002", 22m);
            Lista(1, 1.20m, metodo: 3, regla: "C"); // a 5, arriba → 26.4 → 30
            var r = await _sut.ResolverAsync("0003-0002", 1);
            Assert.Equal(30.00m, r.PrecioLista);
        }

        [Fact]
        public async Task FactorNulo_SeTrataComoUno()
        {
            Articulo("A", 50m);
            Lista(1, null);
            var r = await _sut.ResolverAsync("A", 1);
            Assert.Equal(50.00m, r.PrecioLista);
            Assert.Equal(1m, r.FactorEfectivo);
        }

        [Fact]
        public async Task PrecioUnitarioNulo_BaseCero()
        {
            Articulo("A", null);
            Lista(1, 1.20m);
            var r = await _sut.ResolverAsync("A", 1);
            Assert.Equal(0m, r.PrecioBase);
            Assert.Equal(0.00m, r.PrecioLista);
        }

        [Fact]
        public async Task ArticuloInexistente_LanzaArticuloNoExiste()
        {
            _articulos.Setup(a => a.ObtenerPorCodigoAsync("NOPE")).ReturnsAsync((Articulo)null!);
            await Assert.ThrowsAsync<ArticuloNoExisteException>(() => _sut.ResolverAsync("NOPE", null));
        }

        [Fact]
        public async Task ListaInexistente_LanzaPrecioListaInvalida()
        {
            Articulo("A", 10m);
            _listas.Setup(l => l.ObtenerAsync(999)).ReturnsAsync((ListadoPrecio)null!);
            await Assert.ThrowsAsync<PrecioListaInvalidaException>(() => _sut.ResolverAsync("A", 999));
        }

        [Fact]
        public async Task CicloDirecto_LanzaPrecioListaInvalida()
        {
            Articulo("A", 10m);
            Lista(5, 1.1m, bas: 5); // se apunta a sí misma
            await Assert.ThrowsAsync<PrecioListaInvalidaException>(() => _sut.ResolverAsync("A", 5));
        }

        [Fact]
        public async Task CadenaDe11Niveles_LanzaPrecioListaInvalida()
        {
            Articulo("A", 10m);
            for (int i = 1; i <= 12; i++)
                Lista(i, 1.0m, bas: i < 12 ? i + 1 : (int?)null);
            await Assert.ThrowsAsync<PrecioListaInvalidaException>(() => _sut.ResolverAsync("A", 1));
        }
    }
}
