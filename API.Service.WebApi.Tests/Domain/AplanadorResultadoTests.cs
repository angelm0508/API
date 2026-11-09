using API.Domain.Interface.Reportes;
using API.Service.WebApi.Tests.TestHelpers;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class AplanadorResultadoTests
    {
        private static readonly ColumnaReporte CTexto = new("cliente", "Cliente", TipoColumna.Texto);
        private static readonly ColumnaReporte CImporte = new("importe", "Importe", TipoColumna.Dinero, Totaliza: Totalizacion.Suma);

        private static FilaReporte Fila(string cliente, decimal importe) => ReporteFalso.Fila(("cliente", cliente), ("importe", importe));

        [Fact]
        public void SinAgrupacion_DetalleYTotalGeneral()
        {
            var r = new ResultadoReporte
            {
                Columnas = { CTexto, CImporte },
                Filas = { Fila("A", 10m), Fila("B", 5m) },
                Totales = { ["importe"] = 15m }
            };

            var l = AplanadorResultado.Aplanar(r);

            Assert.Equal(new[] { TipoLinea.Detalle, TipoLinea.Detalle, TipoLinea.Total }, l.Select(x => x.Tipo).ToArray());
            Assert.Equal(new object?[] { "A", 10m }, l[0].Celdas.ToArray());
            Assert.Equal("Total general", l[2].Etiqueta);
            Assert.Equal(new object?[] { null, 15m }, l[2].Celdas.ToArray());
        }

        [Fact]
        public void SinTotales_NoHayLineaDeTotal()
        {
            var r = new ResultadoReporte { Columnas = { CTexto }, Filas = { ReporteFalso.Fila(("cliente", "A")) } };

            var l = AplanadorResultado.Aplanar(r);

            Assert.Single(l);
            Assert.Equal(TipoLinea.Detalle, l[0].Tipo);
        }

        [Fact]
        public void ConAgrupacion_EncabezadoDetalleSubtotalYTotal()
        {
            var grupo = new GrupoReporte
            {
                Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 2,
                Subtotales = { ["importe"] = 15m },
                Filas = { Fila("A", 10m), Fila("A", 5m) }
            };
            var r = new ResultadoReporte { Columnas = { CTexto, CImporte }, Grupos = { grupo }, Totales = { ["importe"] = 15m } };

            var l = AplanadorResultado.Aplanar(r);

            Assert.Equal(new[] { TipoLinea.GrupoEncabezado, TipoLinea.Detalle, TipoLinea.Detalle, TipoLinea.Subtotal, TipoLinea.Total }, l.Select(x => x.Tipo).ToArray());
            Assert.Equal("Cliente: A (2)", l[0].Etiqueta);
            Assert.Equal(0, l[0].Nivel);
            Assert.Equal(1, l[1].Nivel);
            Assert.Equal("Subtotal A", l[3].Etiqueta);
            Assert.Equal(15m, l[3].Celdas[1]);
        }

        [Fact]
        public void DosNiveles_AnidaLosNivelesYLosSubtotales()
        {
            var hoja = new GrupoReporte { Campo = "fecha", Titulo = "Fecha", Etiqueta = "2026-01", Clave = "2026-01", Conteo = 1, Subtotales = { ["importe"] = 10m }, Filas = { Fila("A", 10m) } };
            var raiz = new GrupoReporte { Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 1, Subtotales = { ["importe"] = 10m }, Hijos = { hoja } };
            var r = new ResultadoReporte { Columnas = { CTexto, CImporte }, Grupos = { raiz } };

            var l = AplanadorResultado.Aplanar(r);

            Assert.Equal(new[] { TipoLinea.GrupoEncabezado, TipoLinea.GrupoEncabezado, TipoLinea.Detalle, TipoLinea.Subtotal, TipoLinea.Subtotal }, l.Select(x => x.Tipo).ToArray());
            Assert.Equal(new[] { 0, 1, 2, 1, 0 }, l.Select(x => x.Nivel).ToArray());
        }

        [Fact]
        public void SoloResumen_UnaSolaLineaDeSubtotalPorGrupoHoja()
        {
            var hoja = new GrupoReporte { Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 2, Subtotales = { ["importe"] = 15m } };
            var r = new ResultadoReporte { Columnas = { CTexto, CImporte }, Grupos = { hoja } };

            var l = AplanadorResultado.Aplanar(r);

            var unica = Assert.Single(l);
            Assert.Equal(TipoLinea.Subtotal, unica.Tipo);
            Assert.Equal("Cliente: A (2)", unica.Etiqueta);
            Assert.Equal(15m, unica.Celdas[1]);
        }
    }
}
