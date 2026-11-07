using API.Domain.Core;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class StockAlmacenCalculadoraTests
    {
        [Fact]
        public void Disponible_SinComprometidoNiPedido_EsElStock()
        {
            Assert.Equal(10m, StockAlmacenCalculadora.Disponible(10m, 0m, 0m));
        }

        [Fact]
        public void Disponible_RestaComprometidoYSumaPedido()
        {
            Assert.Equal(11.5m, StockAlmacenCalculadora.Disponible(10m, 3m, 4.5m));
        }

        [Fact]
        public void NivelNecesario_SinMaximo_EsNulo()
        {
            Assert.Null(StockAlmacenCalculadora.NivelNecesario(null, 5m));
        }

        [Fact]
        public void NivelNecesario_ConMaximoCero_EsNulo()
        {
            Assert.Null(StockAlmacenCalculadora.NivelNecesario(0m, 5m));
        }

        [Fact]
        public void NivelNecesario_DisponibleIgualAlMaximo_EsCero()
        {
            Assert.Equal(0m, StockAlmacenCalculadora.NivelNecesario(100m, 100m));
        }

        [Fact]
        public void NivelNecesario_SobreElMaximo_NuncaEsNegativo()
        {
            Assert.Equal(0m, StockAlmacenCalculadora.NivelNecesario(100m, 150m));
        }

        [Fact]
        public void NivelNecesario_PorDebajoDelMaximo_EsLaDiferenciaExactaConSeisDecimales()
        {
            Assert.Equal(0.000001m, StockAlmacenCalculadora.NivelNecesario(10.000002m, 10.000001m));
            Assert.Equal(60.123457m, StockAlmacenCalculadora.NivelNecesario(100m, 39.876543m));
        }

        [Fact]
        public void Estado_SinMinimoNiMaximo_EsOk()
        {
            Assert.Equal("OK", StockAlmacenCalculadora.Estado(5m, null, null));
        }

        [Fact]
        public void Estado_StockIgualAlMinimo_EsOk()
        {
            Assert.Equal("OK", StockAlmacenCalculadora.Estado(10m, 10m, null));
        }

        [Fact]
        public void Estado_StockPorDebajoDelMinimo_EsBajoMinimo()
        {
            Assert.Equal("BAJO_MINIMO", StockAlmacenCalculadora.Estado(9.999999m, 10m, null));
        }

        [Fact]
        public void Estado_MinimoCero_NuncaMarcaBajo()
        {
            Assert.Equal("OK", StockAlmacenCalculadora.Estado(0m, 0m, null));
            Assert.Equal("OK", StockAlmacenCalculadora.Estado(-1m, 0m, null));
        }

        [Fact]
        public void Estado_StockIgualAlMaximo_EsOk()
        {
            Assert.Equal("OK", StockAlmacenCalculadora.Estado(100m, null, 100m));
        }

        [Fact]
        public void Estado_StockSobreElMaximo_EsSobreMaximo()
        {
            Assert.Equal("SOBRE_MAXIMO", StockAlmacenCalculadora.Estado(100.000001m, null, 100m));
        }

        [Fact]
        public void Estado_MaximoCero_NuncaMarcaSobre()
        {
            Assert.Equal("OK", StockAlmacenCalculadora.Estado(5m, null, 0m));
        }

        [Fact]
        public void Estado_ConAmbosYStockDentroDelRango_EsOk()
        {
            Assert.Equal("OK", StockAlmacenCalculadora.Estado(50m, 10m, 100m));
        }
    }
}
