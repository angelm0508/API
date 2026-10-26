using System.ComponentModel.DataAnnotations;
using API.Application.DTO.condicionPago;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    /// <summary>
    /// Las longitudes y el rango de los DTOs de alta/edición de condiciones de pago coinciden con las columnas:
    /// un valor fuera de rango es un 400 con mensaje claro (ModelState de [ApiController]).
    /// </summary>
    public class CondicionPagoDtoLongitudesTests
    {
        private static List<ValidationResult> Validar(object dto)
        {
            var resultados = new List<ValidationResult>();
            Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true);
            return resultados;
        }

        private static string Texto(int largo) => new('x', largo);

        [Fact]
        public void CondicionPagoCrear_CodigoYNombreLargos_SonInvalidos()
        {
            Assert.Empty(Validar(new CondicionPagoCrearDTO { Codigo = Texto(10), Nombre = Texto(50) }));

            var errores = Validar(new CondicionPagoCrearDTO { Codigo = Texto(11), Nombre = Texto(51) });
            Assert.Equal(2, errores.Count);
            Assert.Contains(errores, e => e.ErrorMessage == "El código no puede superar 10 caracteres.");
            Assert.Contains(errores, e => e.ErrorMessage == "El nombre no puede superar 50 caracteres.");
        }

        [Fact]
        public void CondicionPagoActualizar_NombreLargo_EsInvalido()
        {
            Assert.Empty(Validar(new CondicionPagoActualizarDTO { Nombre = Texto(50) }));

            var error = Assert.Single(Validar(new CondicionPagoActualizarDTO { Nombre = Texto(51) }));
            Assert.Equal("El nombre no puede superar 50 caracteres.", error.ErrorMessage);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(365, true)]
        [InlineData(-1, false)]
        [InlineData(366, false)]
        public void CondicionPagoCrear_Dias_EnRango0a365(int dias, bool valido)
        {
            var errores = Validar(new CondicionPagoCrearDTO { Codigo = "C", Nombre = "N", Dias = dias });

            if (valido)
                Assert.Empty(errores);
            else
                Assert.Equal("Los días de crédito deben estar entre 0 y 365.", Assert.Single(errores).ErrorMessage);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(365, true)]
        [InlineData(-1, false)]
        [InlineData(366, false)]
        public void CondicionPagoActualizar_Dias_EnRango0a365(int dias, bool valido)
        {
            var errores = Validar(new CondicionPagoActualizarDTO { Nombre = "N", Dias = dias });

            if (valido)
                Assert.Empty(errores);
            else
                Assert.Equal("Los días de crédito deben estar entre 0 y 365.", Assert.Single(errores).ErrorMessage);
        }
    }
}
