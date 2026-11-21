using API.Domain.Core.Existencias;
using API.Domain.Interface.Existencias;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>Cálculo puro del comprometido/pedido: lo abierto de cada línea en unidad base, sumado por (artículo, almacén).</summary>
    public class CompromisoCalculadoraTests
    {
        [Theory]
        [InlineData(10, 0, 1, 10)]
        [InlineData(10, 4, 1, 6)]
        [InlineData(10, 10, 1, 0)]
        [InlineData(10, 12, 1, 0)]
        [InlineData(3, 1, 12, 24)]
        [InlineData(5, 0, 0, 5)]
        [InlineData(5, 0, -2, 5)]
        public void AbiertoBase_CalculaCantidadAbiertaPorFactor(decimal cantidad, decimal consumido, decimal factor, decimal esperado)
        {
            Assert.Equal(esperado, CompromisoCalculadora.AbiertoBase(cantidad, consumido, factor));
        }

        [Fact]
        public void SumarPorPar_SumaLineasDelMismoPar()
        {
            var lineas = new[]
            {
                new LineaPedidoAbierta("A1", "01", 10m, 0m, 1m),
                new LineaPedidoAbierta("A1", "01", 6m, 0m, 1m),
            };

            var r = CompromisoCalculadora.SumarPorPar(lineas);

            Assert.Single(r);
            Assert.Equal(16m, r[new ParExistencia("A1", "01")]);
        }

        [Fact]
        public void SumarPorPar_MantieneParesDistintosSeparados()
        {
            var lineas = new[]
            {
                new LineaPedidoAbierta("A1", "01", 10m, 0m, 1m),
                new LineaPedidoAbierta("A1", "02", 5m, 0m, 1m),
                new LineaPedidoAbierta("B2", "01", 3m, 1m, 12m),
            };

            var r = CompromisoCalculadora.SumarPorPar(lineas);

            Assert.Equal(3, r.Count);
            Assert.Equal(10m, r[new ParExistencia("A1", "01")]);
            Assert.Equal(5m, r[new ParExistencia("A1", "02")]);
            Assert.Equal(24m, r[new ParExistencia("B2", "01")]);
        }

        [Fact]
        public void SumarPorPar_IgnoraLineasConAbiertoCero()
        {
            var lineas = new[]
            {
                new LineaPedidoAbierta("A1", "01", 10m, 10m, 1m),
                new LineaPedidoAbierta("A2", "01", 10m, 12m, 1m),
                new LineaPedidoAbierta("A3", "01", 4m, 0m, 1m),
            };

            var r = CompromisoCalculadora.SumarPorPar(lineas);

            Assert.Single(r);
            Assert.Equal(4m, r[new ParExistencia("A3", "01")]);
        }

        [Fact]
        public void SumarPorPar_TrataMayusculasYEspaciosComoElMismoPar()
        {
            var lineas = new[]
            {
                new LineaPedidoAbierta("A1", "01", 10m, 0m, 1m),
                new LineaPedidoAbierta("a1", "01 ", 6m, 0m, 1m),
            };

            var r = CompromisoCalculadora.SumarPorPar(lineas);

            Assert.Single(r);
            Assert.Equal(16m, r[new ParExistencia("A1", "01")]);
            Assert.Equal(16m, r[new ParExistencia("a1", "01")]);
        }

        [Fact]
        public void SumarPorPar_SinLineas_DevuelveVacio()
        {
            Assert.Empty(CompromisoCalculadora.SumarPorPar(Array.Empty<LineaPedidoAbierta>()));
        }
    }
}
