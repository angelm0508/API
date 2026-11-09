using API.Domain.Core.Reportes;
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Tests.TestHelpers;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class MotorReportesTests
    {
        internal static readonly ReporteDefinicion Def = new(
            Codigo: "t01", Nombre: "Prueba", Descripcion: "d", Familia: "Pruebas", Modulo: "ReportesVentas",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas),
                new FiltroReporte("cliente", "Cliente", TipoFiltro.Socio, Obligatorio: true),
                new FiltroReporte("tipo", "Tipo", TipoFiltro.Lista, Opciones: new[] { new OpcionFiltro("FAC", "Factura"), new OpcionFiltro("NC", "Nota de crédito") }),
                new FiltroReporte("serie", "Serie", TipoFiltro.Numero)
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha, Agrupable: true),
                new ColumnaReporte("cliente", "Cliente", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("cantidad", "Cantidad", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("importe", "Importe", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("promedio", "Promedio", TipoColumna.Dinero, Totaliza: Totalizacion.Promedio),
                new ColumnaReporte("cuenta", "Cuenta", TipoColumna.Entero, Totaliza: Totalizacion.Conteo),
                new ColumnaReporte("nota", "Nota", TipoColumna.Texto, VisiblePorDefecto: false)
            });

        internal static FilaReporte F(string cliente, DateTime fecha, decimal cantidad, decimal importe, decimal promedio = 0m, string moneda = "QTZ", string? nota = null) =>
            ReporteFalso.Fila(("fecha", fecha), ("cliente", cliente), ("moneda", moneda), ("cantidad", cantidad), ("importe", importe),
                ("promedio", promedio), ("cuenta", 1), ("nota", nota));

        internal static SolicitudReporte Req(Action<SolicitudReporte>? ajustar = null)
        {
            var s = new SolicitudReporte();
            s.Filtros["cliente"] = new FiltroValor { Valor = "C1" };
            ajustar?.Invoke(s);
            return s;
        }

        private static readonly DateTime D1 = new(2026, 1, 10);
        private static readonly DateTime D2 = new(2026, 1, 20);
        private static readonly DateTime D3 = new(2026, 2, 5);

        private readonly MotorReportes _motor = new();

        private static async Task<string> MensajeAsync(Task tarea) =>
            (await Assert.ThrowsAsync<ReporteInvalidoException>(() => tarea)).Message;

        // ---------- filtros ----------

        [Fact]
        public async Task Ejecutar_FiltroDesconocido_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["zzz"] = new FiltroValor { Valor = "1" });

            Assert.Equal("El filtro 'zzz' no es válido para este reporte.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_FiltroObligatorioAusente_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());

            Assert.Equal("El filtro 'Cliente' es obligatorio.", await MensajeAsync(_motor.EjecutarAsync(r, new SolicitudReporte())));
        }

        [Fact]
        public async Task Ejecutar_FiltroObligatorioVacio_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["cliente"] = new FiltroValor { Valor = "   " });

            Assert.Equal("El filtro 'Cliente' es obligatorio.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_FechaConFormatoInvalido_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["fechas"] = new FiltroValor { Desde = "01/02/2026" });

            Assert.Equal("El valor del filtro 'Fechas' no es válido.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_RangoInvertido_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["fechas"] = new FiltroValor { Desde = "2026-02-01", Hasta = "2026-01-01" });

            Assert.Equal("El rango del filtro 'Fechas' no es válido: la fecha inicial es posterior a la final.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_ListaFueraDeLasOpciones_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["tipo"] = new FiltroValor { Valor = "XX" });

            Assert.Equal("El valor del filtro 'Tipo' no es válido.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_NumeroInvalido_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["serie"] = new FiltroValor { Valor = "abc" });

            Assert.Equal("El valor del filtro 'Serie' no es válido.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_FiltroOpcionalVacio_SeIgnora()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["tipo"] = new FiltroValor { Valor = "" });

            await _motor.EjecutarAsync(r, s);

            Assert.Null(r.FiltrosRecibidos!.Texto("tipo"));
        }

        [Fact]
        public async Task Ejecutar_PasaAlReporteLosFiltrosTipadosYElLimiteMasUno()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x =>
            {
                x.Filtros["fechas"] = new FiltroValor { Desde = "2026-01-01", Hasta = "2026-01-31" };
                x.Filtros["serie"] = new FiltroValor { Valor = "7" };
            });

            await _motor.EjecutarAsync(r, s);

            Assert.Equal(MotorReportes.LimiteFilas + 1, r.LimiteRecibido);
            Assert.Equal("C1", r.FiltrosRecibidos!.Texto("cliente"));
            Assert.Equal(new DateTime(2026, 1, 1), r.FiltrosRecibidos.FechaDesde("fechas"));
            Assert.Equal(new DateTime(2026, 2, 1), r.FiltrosRecibidos.FechaHastaExclusiva("fechas"));
            Assert.Equal(7m, r.FiltrosRecibidos.Numero("serie"));
        }

        // ---------- columnas ----------

        [Fact]
        public async Task Ejecutar_SinColumnasPedidas_UsaLasVisiblesPorDefectoEnElOrdenDeLaDefinicion()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 1, 10) });

            var res = await _motor.EjecutarAsync(r, Req());

            Assert.Equal(new[] { "fecha", "cliente", "cantidad", "importe", "promedio", "cuenta" }, res.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal(new[] { "fecha", "cliente", "cantidad", "importe", "promedio", "cuenta" }, res.Filas[0].Keys.ToArray());
        }

        [Fact]
        public async Task Ejecutar_ColumnasPedidas_RespetanElOrdenPedidoYSoloEllas()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 1, 10, nota: "x") });

            var res = await _motor.EjecutarAsync(r, Req(s => s.Columnas.AddRange(new[] { "importe", "fecha", "nota" })));

            Assert.Equal(new[] { "importe", "fecha", "nota" }, res.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal(new[] { "importe", "fecha", "nota" }, res.Filas[0].Keys.ToArray());
            Assert.Equal("x", res.Filas[0]["nota"]);
        }

        [Fact]
        public async Task Ejecutar_ColumnaDesconocida_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());

            Assert.Equal("La columna 'zzz' no es válida para este reporte.",
                await MensajeAsync(_motor.EjecutarAsync(r, Req(s => s.Columnas.Add("zzz")))));
        }

        // ---------- orden ----------

        [Fact]
        public async Task Ejecutar_OrdenDescendentePorImporte()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 1, 10), F("C1", D2, 1, 30), F("C1", D3, 1, 20) });

            var res = await _motor.EjecutarAsync(r, Req(s => s.Orden.Add(new OrdenReporte { Campo = "importe", Descendente = true })));

            Assert.Equal(new object?[] { 30m, 20m, 10m }, res.Filas.Select(f => f["importe"]).ToArray());
        }

        [Fact]
        public async Task Ejecutar_OrdenMultiple_UsaElSegundoCriterioParaDesempatar()
        {
            var r = new ReporteFalso(Def, new[] { F("B", D1, 1, 5), F("A", D2, 1, 7), F("A", D1, 1, 9) });

            var res = await _motor.EjecutarAsync(r, Req(s =>
            {
                s.Orden.Add(new OrdenReporte { Campo = "cliente" });
                s.Orden.Add(new OrdenReporte { Campo = "importe", Descendente = true });
            }));

            Assert.Equal(new object?[] { 9m, 7m, 5m }, res.Filas.Select(f => f["importe"]).ToArray());
        }

        [Fact]
        public async Task Ejecutar_SinOrdenPedido_ConservaElOrdenNaturalDelReporte()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D2, 1, 2), F("C1", D1, 1, 1) });

            var res = await _motor.EjecutarAsync(r, Req());

            Assert.Equal(new object?[] { 2m, 1m }, res.Filas.Select(f => f["importe"]).ToArray());
        }

        [Fact]
        public async Task Ejecutar_OrdenPorColumnaDesconocida_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Orden.Add(new OrdenReporte { Campo = "zzz" }));

            Assert.Equal("No se puede ordenar por 'zzz': la columna no es válida para este reporte.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_MasDeTresCriteriosDeOrden_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Orden.AddRange(new[] { "fecha", "cliente", "importe", "cantidad" }.Select(c => new OrdenReporte { Campo = c })));

            Assert.Equal("Solo se permiten hasta 3 criterios de orden.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        // ---------- truncado ----------

        private static IEnumerable<FilaReporte> Filas(int n) =>
            Enumerable.Range(1, n).Select(i => F("C1", D1, 1, 1));

        [Fact]
        public async Task Ejecutar_ExactamenteElTope_NoSeTrunca()
        {
            var res = await _motor.EjecutarAsync(new ReporteFalso(Def, Filas(MotorReportes.LimiteFilas)), Req());

            Assert.False(res.Truncado);
            Assert.Equal(MotorReportes.LimiteFilas, res.TotalFilas);
            Assert.Equal(MotorReportes.LimiteFilas, res.Filas.Count);
        }

        [Fact]
        public async Task Ejecutar_UnaFilaMasQueElTope_SeTruncaAlTope()
        {
            var res = await _motor.EjecutarAsync(new ReporteFalso(Def, Filas(MotorReportes.LimiteFilas + 5)), Req());

            Assert.True(res.Truncado);
            Assert.Equal(MotorReportes.LimiteFilas, res.TotalFilas);
            Assert.Equal(MotorReportes.LimiteFilas, res.Filas.Count);
        }

        // ---------- totales ----------

        [Fact]
        public async Task Ejecutar_Totales_SumaPromedioYConteo()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 2, 10, promedio: 1), F("C1", D2, 3, 20, promedio: 2), F("C1", D3, 5, 30, promedio: 6) });

            var res = await _motor.EjecutarAsync(r, Req());

            Assert.Equal(10m, res.Totales["cantidad"]);
            Assert.Equal(60m, res.Totales["importe"]);
            Assert.Equal(3m, res.Totales["promedio"]);
            Assert.Equal(3m, res.Totales["cuenta"]);
            Assert.False(res.Totales.ContainsKey("fecha"));
        }

        [Fact]
        public async Task Ejecutar_Totales_SoloParaColumnasVisibles()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 2, 10) });

            var res = await _motor.EjecutarAsync(r, Req(s => s.Columnas.AddRange(new[] { "fecha", "cliente" })));

            Assert.Empty(res.Totales);
        }

        [Fact]
        public async Task Ejecutar_SinFilas_SumaCeroPromedioNuloYConteoCero()
        {
            var res = await _motor.EjecutarAsync(new ReporteFalso(Def, new List<FilaReporte>()), Req());

            Assert.Equal(0m, res.Totales["importe"]);
            Assert.Null(res.Totales["promedio"]);
            Assert.Equal(0m, res.Totales["cuenta"]);
            Assert.Equal(0, res.TotalFilas);
            Assert.Empty(res.Filas);
        }

        // ---------- moneda ----------

        [Fact]
        public async Task Ejecutar_VariasMonedas_AgregaLaColumnaMonedaNoSumaImportesYAvisa()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 2, 10, moneda: "QTZ"), F("C1", D2, 3, 20, moneda: "USD") });

            var res = await _motor.EjecutarAsync(r, Req());

            Assert.Contains(res.Columnas, c => c.Campo == "moneda");
            Assert.Null(res.Totales["importe"]);
            Assert.Null(res.Totales["promedio"]);
            Assert.Equal(5m, res.Totales["cantidad"]);
            Assert.Contains("El resultado incluye más de una moneda: los importes no se suman.", res.Avisos);
        }

        [Fact]
        public async Task Ejecutar_UnaSolaMoneda_NoAgregaLaColumnaNiAviso()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 2, 10), F("C1", D2, 3, 20) });

            var res = await _motor.EjecutarAsync(r, Req());

            Assert.DoesNotContain(res.Columnas, c => c.Campo == "moneda");
            Assert.Equal(30m, res.Totales["importe"]);
            Assert.Empty(res.Avisos);
        }
    }
}
