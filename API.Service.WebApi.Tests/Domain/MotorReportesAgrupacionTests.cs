using API.Domain.Core.Reportes;
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Tests.TestHelpers;
using Xunit;
using static API.Service.WebApi.Tests.Domain.MotorReportesTests;

namespace API.Service.WebApi.Tests.Domain
{
    public class MotorReportesAgrupacionTests
    {
        private static readonly DateTime E10 = new(2026, 1, 10);
        private static readonly DateTime E20 = new(2026, 1, 20);
        private static readonly DateTime F5 = new(2026, 2, 5);

        private readonly MotorReportes _motor = new();

        private static SolicitudReporte Agrupado(Action<SolicitudReporte> ajustar) => Req(ajustar);

        private static AgrupacionReporte Por(string campo, ParteFecha? parte = null) => new() { Campo = campo, Parte = parte };

        private static async Task<string> MensajeAsync(Task tarea) =>
            (await Assert.ThrowsAsync<ReporteInvalidoException>(() => tarea)).Message;

        [Fact]
        public async Task AgruparPorTexto_DaUnGrupoPorValorConSubtotalesYFilas()
        {
            var r = new ReporteFalso(Def, new[] { F("B", E10, 1, 5), F("A", E10, 2, 10), F("A", E20, 3, 20) });

            var res = await _motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("cliente"))));

            Assert.Empty(res.Filas);
            Assert.Equal(new[] { "A", "B" }, res.Grupos.Select(g => g.Etiqueta).ToArray());
            var a = res.Grupos[0];
            Assert.Equal("cliente", a.Campo);
            Assert.Equal("Cliente", a.Titulo);
            Assert.Equal(2, a.Conteo);
            Assert.Equal(30m, a.Subtotales["importe"]);
            Assert.Equal(5m, a.Subtotales["cantidad"]);
            Assert.Equal(2, a.Filas.Count);
            Assert.Equal(35m, res.Totales["importe"]);
        }

        [Fact]
        public async Task SoloResumen_DejaLosGruposSinFilas()
        {
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 10), F("A", E20, 1, 20) });

            var res = await _motor.EjecutarAsync(r, Agrupado(s => { s.Agrupar.Add(Por("cliente")); s.SoloResumen = true; }));

            Assert.Single(res.Grupos);
            Assert.Empty(res.Grupos[0].Filas);
            Assert.Equal(30m, res.Grupos[0].Subtotales["importe"]);
            Assert.Equal(2, res.Grupos[0].Conteo);
        }

        [Fact]
        public async Task DosNiveles_ClienteYLuegoMes_FormaElArbolYLasFilasCuelganDeLasHojas()
        {
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 10), F("A", E20, 1, 20), F("A", F5, 1, 40), F("B", E10, 1, 5) });

            var res = await _motor.EjecutarAsync(r, Agrupado(s => { s.Agrupar.Add(Por("cliente")); s.Agrupar.Add(Por("fecha", ParteFecha.Mes)); }));

            var a = res.Grupos.Single(g => g.Etiqueta == "A");
            Assert.Empty(a.Filas);
            Assert.Equal(new[] { "2026-01", "2026-02" }, a.Hijos.Select(h => h.Etiqueta).ToArray());
            Assert.Equal(30m, a.Hijos[0].Subtotales["importe"]);
            Assert.Equal(2, a.Hijos[0].Filas.Count);
            Assert.Equal(70m, a.Subtotales["importe"]);
        }

        [Fact]
        public async Task FechaPorAnio_YPorDia_UsanSusFormatos()
        {
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 1), F("A", F5, 1, 1) });

            var porAnio = await _motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("fecha", ParteFecha.Anio))));
            var porDia = await _motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("fecha"))));

            Assert.Equal(new[] { "2026" }, porAnio.Grupos.Select(g => g.Etiqueta).ToArray());
            Assert.Equal(new[] { "2026-01-10", "2026-02-05" }, porDia.Grupos.Select(g => g.Etiqueta).ToArray());
        }

        [Fact]
        public async Task ValorNulo_VaEnUnGrupoSinValorAntesQueLosDemas()
        {
            var sinCliente = ReporteFalso.Fila(("fecha", E10), ("cliente", null), ("moneda", "QTZ"), ("cantidad", 1m), ("importe", 5m), ("promedio", 0m), ("cuenta", 1), ("nota", null));
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 1), sinCliente });

            var res = await _motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("cliente"))));

            Assert.Equal(new[] { "(Sin valor)", "A" }, res.Grupos.Select(g => g.Etiqueta).ToArray());
        }

        [Fact]
        public async Task CampoDeAgrupacionNoVisible_IgualAgrupa()
        {
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 10), F("B", E10, 1, 5) });

            var res = await _motor.EjecutarAsync(r, Agrupado(s => { s.Columnas.Add("importe"); s.Agrupar.Add(Por("cliente")); }));

            Assert.Equal(new[] { "A", "B" }, res.Grupos.Select(g => g.Etiqueta).ToArray());
            Assert.Equal(new[] { "importe" }, res.Grupos[0].Filas[0].Keys.ToArray());
        }

        [Fact]
        public async Task LasFilasDeUnGrupoRespetanElOrdenPedido()
        {
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 5), F("A", E20, 1, 9), F("A", F5, 1, 7) });

            var res = await _motor.EjecutarAsync(r, Agrupado(s =>
            {
                s.Agrupar.Add(Por("cliente"));
                s.Orden.Add(new OrdenReporte { Campo = "importe", Descendente = true });
            }));

            Assert.Equal(new object?[] { 9m, 7m, 5m }, res.Grupos[0].Filas.Select(f => f["importe"]).ToArray());
        }

        [Fact]
        public async Task ColumnaNoAgrupable_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());

            Assert.Equal("No se puede agrupar por 'Nota': la columna no es agrupable.",
                await MensajeAsync(_motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("nota"))))));
        }

        [Fact]
        public async Task ColumnaDesconocida_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());

            Assert.Equal("No se puede agrupar por 'zzz': la columna no es válida para este reporte.",
                await MensajeAsync(_motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("zzz"))))));
        }

        [Fact]
        public async Task MasDeDosNiveles_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());

            Assert.Equal("Solo se permiten hasta 2 niveles de agrupación.",
                await MensajeAsync(_motor.EjecutarAsync(r, Agrupado(s => { s.Agrupar.Add(Por("cliente")); s.Agrupar.Add(Por("moneda")); s.Agrupar.Add(Por("fecha")); }))));
        }

        [Fact]
        public async Task ParteDeFechaEnColumnaQueNoEsFecha_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());

            Assert.Equal("La parte de fecha solo aplica a columnas de tipo fecha.",
                await MensajeAsync(_motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("cliente", ParteFecha.Mes))))));
        }

        [Fact]
        public async Task VariasMonedas_NoSumaImportesEnLosSubtotales()
        {
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 10, moneda: "QTZ"), F("A", E20, 1, 20, moneda: "USD") });

            var res = await _motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("cliente"))));

            Assert.Null(res.Grupos[0].Subtotales["importe"]);
            Assert.Equal(2m, res.Grupos[0].Subtotales["cantidad"]);
        }
    }
}
