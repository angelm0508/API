using API.Domain.Core;
using API.Domain.Interface;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ValuacionInventarioTests
    {
        private readonly IValuacionInventario _v = new ValuacionInventario();

        [Fact]
        public void CalcularEntrada_Promedio_PrimeraEntrada_CostoIgualAlPrecio()
        {
            var r = _v.CalcularEntrada(cantActual: 0m, costoPromActual: 0m, costoEstandar: 0m, metodo: "P", cantidad: 10m, precioUnitario: 25m);
            Assert.Equal(25m, r.NuevoCostoPromedio);
            Assert.Equal(25m, r.CostoUnitarioMov);
            Assert.Equal(250m, r.ValorMovimiento);
            Assert.Equal(0m, r.VariacionPrecio);
        }

        [Fact]
        public void CalcularEntrada_Promedio_SegundaEntrada_PromediaPonderado()
        {
            // 10 @ 25 ya en stock; entra 5 @ 30 => (250 + 150) / 15 = 26.666...
            var r = _v.CalcularEntrada(cantActual: 10m, costoPromActual: 25m, costoEstandar: 0m, metodo: "P", cantidad: 5m, precioUnitario: 30m);
            Assert.Equal(400m / 15m, r.NuevoCostoPromedio);
            Assert.Equal(30m, r.CostoUnitarioMov);
            Assert.Equal(150m, r.ValorMovimiento);
            Assert.Equal(0m, r.VariacionPrecio);
        }

        [Fact]
        public void CalcularEntrada_Promedio_TotalCero_ConservaElCostoActual()
        {
            var r = _v.CalcularEntrada(cantActual: -5m, costoPromActual: 12m, costoEstandar: 0m, metodo: "P", cantidad: 5m, precioUnitario: 99m);
            Assert.Equal(12m, r.NuevoCostoPromedio);
        }

        [Fact]
        public void CalcularEntrada_Estandar_ValuaAlEstandar_YRegistraVariacion()
        {
            // estandar 20; se recibe 10 @ 25 => stock vale 10*20; variacion 10*(25-20) = 50
            var r = _v.CalcularEntrada(cantActual: 3m, costoPromActual: 20m, costoEstandar: 20m, metodo: "E", cantidad: 10m, precioUnitario: 25m);
            Assert.Equal(20m, r.NuevoCostoPromedio);
            Assert.Equal(20m, r.CostoUnitarioMov);
            Assert.Equal(200m, r.ValorMovimiento);
            Assert.Equal(50m, r.VariacionPrecio);
        }

        [Fact]
        public void CalcularEntrada_Estandar_PrecioMenorAlEstandar_VariacionNegativa()
        {
            var r = _v.CalcularEntrada(cantActual: 0m, costoPromActual: 0m, costoEstandar: 20m, metodo: "E", cantidad: 4m, precioUnitario: 18m);
            Assert.Equal(-8m, r.VariacionPrecio);
        }

        [Fact]
        public void CalcularSalida_Promedio_ValuaAlCostoPromedio_NoRecalcula()
        {
            var r = _v.CalcularSalida(cantActual: 15m, costoPromActual: 26m, costoEstandar: 0m, metodo: "P", cantidad: 4m);
            Assert.Equal(26m, r.NuevoCostoPromedio);
            Assert.Equal(26m, r.CostoUnitarioMov);
            Assert.Equal(-104m, r.ValorMovimiento);
            Assert.Equal(0m, r.VariacionPrecio);
        }

        [Fact]
        public void CalcularSalida_Estandar_ValuaAlEstandar()
        {
            var r = _v.CalcularSalida(cantActual: 15m, costoPromActual: 26m, costoEstandar: 20m, metodo: "E", cantidad: 4m);
            Assert.Equal(20m, r.CostoUnitarioMov);
            Assert.Equal(-80m, r.ValorMovimiento);
        }
    }
}
