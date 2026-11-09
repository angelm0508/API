using API.Domain.Interface.Reportes;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class FiltrosReporteTests
    {
        private static FiltrosReporte Con(string campo, FiltroValor valor) =>
            new(new Dictionary<string, FiltroValor> { [campo] = valor });

        [Fact]
        public void Texto_RecortaYDevuelveNuloSiEstaVacioOAusente()
        {
            Assert.Equal("C1", Con("c", new FiltroValor { Valor = "  C1 " }).Texto("c"));
            Assert.Null(Con("c", new FiltroValor { Valor = "   " }).Texto("c"));
            Assert.Null(FiltrosReporte.Vacio.Texto("c"));
        }

        [Fact]
        public void Numero_UsaCulturaInvarianteYDevuelveNuloSiNoEsNumero()
        {
            Assert.Equal(12.5m, Con("n", new FiltroValor { Valor = "12.5" }).Numero("n"));
            Assert.Null(Con("n", new FiltroValor { Valor = "abc" }).Numero("n"));
            Assert.Null(FiltrosReporte.Vacio.Numero("n"));
        }

        [Fact]
        public void Fecha_AceptaSoloAnioMesDia()
        {
            Assert.Equal(new DateTime(2026, 1, 31), Con("f", new FiltroValor { Valor = "2026-01-31" }).Fecha("f"));
            Assert.Null(Con("f", new FiltroValor { Valor = "31/01/2026" }).Fecha("f"));
            Assert.Null(FiltrosReporte.ParseFecha(null));
        }

        [Fact]
        public void Rango_DesdeEsElDiaYHastaEsExclusivoElDiaSiguiente()
        {
            var f = Con("r", new FiltroValor { Desde = "2026-01-01", Hasta = "2026-01-31" });

            Assert.Equal(new DateTime(2026, 1, 1), f.FechaDesde("r"));
            Assert.Equal(new DateTime(2026, 2, 1), f.FechaHastaExclusiva("r"));
        }

        [Fact]
        public void Rango_SinExtremosDevuelveNulo()
        {
            var f = Con("r", new FiltroValor());

            Assert.Null(f.FechaDesde("r"));
            Assert.Null(f.FechaHastaExclusiva("r"));
            Assert.Null(FiltrosReporte.Vacio.FechaDesde("r"));
        }
    }
}
