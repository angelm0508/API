using API.Domain.Core.Precio;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class RedondeoCalculadoraTests
    {
        public static TheoryData<decimal, short?, string?, decimal> Casos => new()
        {
            { 26.4m, 0,    "R", 26.40m },  // sin múltiplo
            { 26.4m, null, "R", 26.40m },  // método nulo
            { 26.4m, 1,    "R", 26.00m },  // a la unidad, al más cercano
            { 26.4m, 1,    "C", 27.00m },  // a la unidad, arriba
            { 26.4m, 1,    "F", 26.00m },  // a la unidad, abajo
            { 26.4m, 2,    "R", 26.50m },  // a 0.5
            { 26.4m, 3,    "R", 25.00m },  // a 5, al más cercano
            { 26.4m, 3,    "C", 30.00m },  // a 5, arriba
            { 26.4m, 3,    "F", 25.00m },  // a 5, abajo
            { 12.5m, 2,    "R", 12.50m },  // ya es múltiplo de 0.5
            { 24.0m, 5,    "C", 25.00m },  // a 25, arriba
            { 26.4m, 0,    null, 26.40m }, // regla nula con método 0
        };

        [Theory]
        [MemberData(nameof(Casos))]
        public void Redondear_SegunMetodoYRegla(decimal valor, short? metodo, string? regla, decimal esperado)
        {
            Assert.Equal(esperado, RedondeoCalculadora.Redondear(valor, metodo, regla));
        }
    }
}
