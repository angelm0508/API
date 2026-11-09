using System.Text.Json;
using API.Domain.Interface.Reportes;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ContratosReporteTests
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        [Fact]
        public void LosEnumsSeSerializanComoTexto()
        {
            var json = JsonSerializer.Serialize(new ColumnaReporte("total", "Total", TipoColumna.Dinero, Totaliza: Totalizacion.Suma), Web);

            Assert.Contains("\"tipo\":\"Dinero\"", json);
            Assert.Contains("\"totaliza\":\"Suma\"", json);
        }

        [Fact]
        public void LaSolicitudSeLeeDesdeJsonConEnumsComoTexto()
        {
            const string json = """
                { "filtros": { "fechas": { "desde": "2026-01-01", "hasta": "2026-01-31" }, "cliente": { "valor": "C1" } },
                  "columnas": ["fecha","total"],
                  "orden": [ { "campo": "total", "descendente": true } ],
                  "agrupar": [ { "campo": "fecha", "parte": "Mes" } ],
                  "soloResumen": true }
                """;

            var s = JsonSerializer.Deserialize<SolicitudReporte>(json, Web)!;

            Assert.Equal("C1", s.Filtros["cliente"].Valor);
            Assert.Equal("2026-01-31", s.Filtros["fechas"].Hasta);
            Assert.Equal(new[] { "fecha", "total" }, s.Columnas);
            Assert.True(s.Orden[0].Descendente);
            Assert.Equal(ParteFecha.Mes, s.Agrupar[0].Parte);
            Assert.True(s.SoloResumen);
        }

        [Fact]
        public void LaSolicitudVaciaTieneColeccionesNoNulas()
        {
            var s = new SolicitudReporte();

            Assert.Empty(s.Filtros);
            Assert.Empty(s.Columnas);
            Assert.Empty(s.Orden);
            Assert.Empty(s.Agrupar);
            Assert.False(s.SoloResumen);
        }

        [Fact]
        public void LaFilaSerializaSusValoresPorTipoEnTiempoDeEjecucion()
        {
            var fila = new FilaReporte { ["fecha"] = new DateTime(2026, 1, 5), ["total"] = 12.5m, ["cliente"] = "C1", ["nota"] = null };

            var json = JsonSerializer.Serialize(fila, Web);

            Assert.Contains("\"total\":12.5", json);
            Assert.Contains("\"cliente\":\"C1\"", json);
            Assert.Contains("\"nota\":null", json);
        }

        [Fact]
        public void ReporteNoExiste_LlevaElMensajeConElCodigo()
        {
            Assert.Equal("El reporte 'x' no existe.", new ReporteNoExisteException("x").Message);
        }
    }
}
