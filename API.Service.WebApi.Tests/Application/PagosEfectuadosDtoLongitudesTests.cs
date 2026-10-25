using System.ComponentModel.DataAnnotations;
using API.Application.DTO.pagoEfectuado;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    /// <summary>
    /// Las longitudes de los DTOs de alta de pagos efectuados coinciden con las columnas: un texto largo es un 400 con
    /// mensaje claro (ModelState de [ApiController]) y no la excepción genérica de truncado de EF.
    /// </summary>
    public class PagosEfectuadosDtoLongitudesTests
    {
        private static List<ValidationResult> Validar(object dto)
        {
            var resultados = new List<ValidationResult>();
            Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true);
            return resultados;
        }

        private static string Texto(int largo) => new('x', largo);

        [Fact]
        public void PagoEfectuadoCrear_ComentarioDe254_EsValido_YDe255_NoLoEs()
        {
            Assert.Empty(Validar(new PagoEfectuadoCrearDTO { Comentario = Texto(254) }));

            var error = Assert.Single(Validar(new PagoEfectuadoCrearDTO { Comentario = Texto(255) }));
            Assert.Contains(nameof(PagoEfectuadoCrearDTO.Comentario), error.MemberNames);
            Assert.Equal("El comentario no puede superar 254 caracteres.", error.ErrorMessage);
        }

        [Fact]
        public void PagoEfectuadoMedioCrear_ReferenciaYBancoLargos_SonInvalidos()
        {
            Assert.Empty(Validar(new PagoEfectuadoMedioCrearDTO { Referencia = Texto(50), Banco = Texto(60) }));

            var errores = Validar(new PagoEfectuadoMedioCrearDTO { Referencia = Texto(51), Banco = Texto(61) });
            Assert.Equal(2, errores.Count);
            Assert.Contains(errores, e => e.ErrorMessage == "La referencia no puede superar 50 caracteres.");
            Assert.Contains(errores, e => e.ErrorMessage == "El banco no puede superar 60 caracteres.");
        }
    }
}
