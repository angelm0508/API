using API.Domain.Core;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>Vencimiento editable al crear: qué fecha se guarda según lo pedido, lo calculado y la fecha del documento.</summary>
    public class VencimientoDocumentoTests
    {
        private static readonly DateTime Calculado = new(2026, 3, 2);
        private static readonly DateTime FechaDoc = new(2026, 1, 31);

        [Fact]
        public void SinFechaPedida_DevuelveLaCalculada()
        {
            Assert.Equal(Calculado, VencimientoDocumento.Aplicar(null, Calculado, FechaDoc));
        }

        [Fact]
        public void FechaPedidaPosterior_DevuelveLaPedida()
        {
            Assert.Equal(new DateTime(2026, 6, 1), VencimientoDocumento.Aplicar(new DateTime(2026, 6, 1), Calculado, FechaDoc));
        }

        [Fact]
        public void FechaPedidaDelMismoDia_Vale_AunqueLaHoraSeaMenor()
        {
            var resultado = VencimientoDocumento.Aplicar(new DateTime(2026, 1, 31, 0, 0, 0), Calculado, new DateTime(2026, 1, 31, 18, 45, 0));

            Assert.Equal(new DateTime(2026, 1, 31), resultado);
        }

        [Fact]
        public void FechaPedidaAnterior_LanzaConElMensajeLiteral()
        {
            var ex = Assert.Throws<Exception>(() => VencimientoDocumento.Aplicar(new DateTime(2026, 1, 30), Calculado, FechaDoc));

            Assert.Equal("La fecha de vencimiento no puede ser anterior a la fecha del documento.", ex.Message);
        }

        [Fact]
        public void FechaPedidaDelDiaAnteriorConHoraTardia_Lanza_ComparaPorDia()
        {
            Assert.Throws<Exception>(() => VencimientoDocumento.Aplicar(new DateTime(2026, 1, 30, 23, 59, 59), Calculado, new DateTime(2026, 1, 31, 0, 0, 1)));
        }

        [Fact]
        public void QuitaLaHoraDeLaFechaPedida()
        {
            var resultado = VencimientoDocumento.Aplicar(new DateTime(2026, 5, 10, 14, 30, 15), Calculado, FechaDoc);

            Assert.Equal(new DateTime(2026, 5, 10), resultado);
            Assert.Equal(TimeSpan.Zero, resultado.TimeOfDay);
        }

        [Fact]
        public void SinFechaDeDocumento_ComparaContraHoy()
        {
            var ayer = DateTime.Today.AddDays(-1);

            Assert.Throws<Exception>(() => VencimientoDocumento.Aplicar(ayer, Calculado, null));
            Assert.Equal(DateTime.Today, VencimientoDocumento.Aplicar(DateTime.Today, Calculado, null));
        }

        [Fact]
        public void SinFechaPedida_NoValidaContraLaDelDocumento()
        {
            // Comportamiento previo: lo calculado se devuelve tal cual, aunque sea anterior (p. ej. condición de días negativos no existe, pero no se valida aquí).
            Assert.Equal(new DateTime(2020, 1, 1), VencimientoDocumento.Aplicar(null, new DateTime(2020, 1, 1), FechaDoc));
        }
    }
}
