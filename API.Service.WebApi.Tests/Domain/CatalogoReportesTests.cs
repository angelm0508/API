using API.Domain.Core.Reportes;
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Tests.TestHelpers;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class CatalogoReportesTests
    {
        private static ReporteFalso R(string codigo, string nombre, string familia) =>
            new(new ReporteDefinicion(codigo, nombre, "d", familia, "ReportesVentas", Array.Empty<FiltroReporte>(), Array.Empty<ColumnaReporte>()),
                new List<FilaReporte>());

        [Fact]
        public void Todos_SeOrdenaPorFamiliaYNombre()
        {
            var c = new CatalogoReportes(new[] { R("c", "Zeta", "Ventas"), R("a", "Alfa", "Ventas"), R("b", "Beta", "Compras") });

            Assert.Equal(new[] { "b", "a", "c" }, c.Todos.Select(r => r.Definicion.Codigo).ToArray());
        }

        [Fact]
        public void Buscar_NoDistingueMayusculasYDevuelveNuloSiNoExiste()
        {
            var c = new CatalogoReportes(new[] { R("ventas-por-documento", "V", "Ventas") });

            Assert.NotNull(c.Buscar("VENTAS-POR-DOCUMENTO"));
            Assert.Null(c.Buscar("zzz"));
        }

        [Fact]
        public void CodigosDuplicados_LanzanAlConstruir()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new CatalogoReportes(new[] { R("x", "A", "F"), R("X", "B", "F") }));

            Assert.Equal("Código de reporte duplicado: x", ex.Message);
        }
    }
}
