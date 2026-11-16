using API.Domain.Core;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>NIT del documento al crear: se recorta, vacío = null y máximo 15 caracteres.</summary>
    public class NitDocumentoTests
    {
        [Theory]
        [InlineData("1234567-8", "1234567-8")]
        [InlineData("  1234567-8  ", "1234567-8")]
        [InlineData("CF", "CF")]
        [InlineData("\t12345\r\n", "12345")]
        public void ConValor_LoRecorta(string nit, string esperado)
        {
            Assert.Equal(esperado, NitDocumento.Normalizar(nit));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\r\n")]
        public void VacioOSoloEspacios_EsNulo(string? nit)
        {
            Assert.Null(NitDocumento.Normalizar(nit));
        }

        [Fact]
        public void QuinceCaracteres_Vale()
        {
            Assert.Equal("123456789012345", NitDocumento.Normalizar("123456789012345"));
        }

        [Fact]
        public void QuinceCaracteresConEspaciosAlrededor_Vale_PorqueSeMideElRecortado()
        {
            Assert.Equal("123456789012345", NitDocumento.Normalizar("  123456789012345  "));
        }

        [Fact]
        public void DieciseisCaracteres_Lanza()
        {
            var ex = Assert.Throws<Exception>(() => NitDocumento.Normalizar("1234567890123456"));

            Assert.Equal("El NIT no puede tener más de 15 caracteres.", ex.Message);
            Assert.Equal(NitDocumento.MensajeExcedeLongitud, ex.Message);
        }
    }
}
