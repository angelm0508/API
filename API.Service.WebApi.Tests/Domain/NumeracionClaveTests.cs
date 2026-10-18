using API.Domain.Entity;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class NumeracionClaveTests
    {
        [Theory]
        [InlineData("6", null, "6")]
        [InlineData("6", "", "6")]
        [InlineData("6", "ND", "6:ND")]
        [InlineData("13", "DN", "13:DN")]
        public void Componer_UsaSoloElCodigoSiNoHaySubtipo(string codigo, string? subTipo, string esperado)
        {
            Assert.Equal(esperado, NumeracionClave.Componer(codigo, subTipo));
        }

        [Theory]
        [InlineData("6", "6", null)]
        [InlineData("6:ND", "6", "ND")]
        [InlineData("13:DN", "13", "DN")]
        [InlineData("1:--", "1", "--")]
        [InlineData("6:", "6", null)]
        public void Separar_DevuelveCodigoYSubtipo(string clave, string codigo, string? subTipo)
        {
            Assert.Equal((codigo, subTipo), NumeracionClave.Separar(clave));
        }
    }
}
