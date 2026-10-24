using System.ComponentModel.DataAnnotations;
using API.Application.DTO.formaPago;
using API.Application.DTO.pagoRecibido;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    /// <summary>
    /// Las longitudes de los DTOs de alta coinciden con las columnas: un texto largo es un 400 con mensaje claro
    /// (ModelState de [ApiController]) y no la excepción genérica de truncado de EF.
    /// </summary>
    public class PagosDtoLongitudesTests
    {
        private static List<ValidationResult> Validar(object dto)
        {
            var resultados = new List<ValidationResult>();
            Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true);
            return resultados;
        }

        private static string Texto(int largo) => new('x', largo);

        [Fact]
        public void PagoRecibidoCrear_ComentarioDe254_EsValido_YDe255_NoLoEs()
        {
            Assert.Empty(Validar(new PagoRecibidoCrearDTO { Comentario = Texto(254) }));

            var error = Assert.Single(Validar(new PagoRecibidoCrearDTO { Comentario = Texto(255) }));
            Assert.Contains(nameof(PagoRecibidoCrearDTO.Comentario), error.MemberNames);
            Assert.Equal("El comentario no puede superar 254 caracteres.", error.ErrorMessage);
        }

        [Fact]
        public void PagoRecibidoMedioCrear_ReferenciaYBancoLargos_SonInvalidos()
        {
            Assert.Empty(Validar(new PagoRecibidoMedioCrearDTO { Referencia = Texto(50), Banco = Texto(60) }));

            var errores = Validar(new PagoRecibidoMedioCrearDTO { Referencia = Texto(51), Banco = Texto(61) });
            Assert.Equal(2, errores.Count);
            Assert.Contains(errores, e => e.ErrorMessage == "La referencia no puede superar 50 caracteres.");
            Assert.Contains(errores, e => e.ErrorMessage == "El banco no puede superar 60 caracteres.");
        }

        [Fact]
        public void FormaPagoCrear_CodigoYNombreLargos_SonInvalidos()
        {
            Assert.Empty(Validar(new FormaPagoCrearDTO { Codigo = Texto(10), Nombre = Texto(50) }));

            var errores = Validar(new FormaPagoCrearDTO { Codigo = Texto(11), Nombre = Texto(51) });
            Assert.Equal(2, errores.Count);
            Assert.Contains(errores, e => e.ErrorMessage == "El código no puede superar 10 caracteres.");
            Assert.Contains(errores, e => e.ErrorMessage == "El nombre no puede superar 50 caracteres.");
        }

        [Fact]
        public void FormaPagoActualizar_NombreLargo_EsInvalido()
        {
            Assert.Empty(Validar(new FormaPagoActualizarDTO { Nombre = Texto(50) }));

            var error = Assert.Single(Validar(new FormaPagoActualizarDTO { Nombre = Texto(51) }));
            Assert.Equal("El nombre no puede superar 50 caracteres.", error.ErrorMessage);
        }
    }
}
