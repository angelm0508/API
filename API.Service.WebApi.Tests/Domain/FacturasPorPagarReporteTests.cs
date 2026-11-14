using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class FacturasPorPagarReporteTests
    {
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoFacturas = new();
        private readonly Mock<ISaldoFacturaCompraService> _saldos = new();
        private readonly List<int> _tamanosLote = new();

        /// <summary>Crea el reporte; <paramref name="saldos"/> es lo que "calcula" el servicio (una factura ausente se omite).</summary>
        private FacturasPorPagarReporte Crear(FacturaCompra[] facturas, params SaldoFacturaCompra[] saldos)
        {
            _repoFacturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync((IEnumerable<int> ids) =>
                {
                    var lista = ids.ToList();
                    _tamanosLote.Add(lista.Count);
                    return (IReadOnlyDictionary<int, SaldoFacturaCompra>)saldos.Where(s => lista.Contains(s.FacturaCompraEntry)).ToDictionary(s => s.FacturaCompraEntry);
                });
            return new FacturasPorPagarReporte(_repoFacturas.Object, _saldos.Object);
        }

        private static FacturaCompra Fac(int entry, DateTime fecha, DateTime? vence = null, string cliente = "P1", string sub = "--",
            string cancelado = "N", decimal total = 100m, int? numDoc = null, string moneda = "QTZ") =>
            new()
            {
                Entry = entry, NumDoc = numDoc ?? entry, Serie = 1, Cancelado = cancelado, DocSubType = sub, FechaDoc = fecha,
                FechaVencimiento = vence, CodigoSn = cliente, NombreSn = "Proveedor " + cliente, MonedaDoc = moneda, TotalDoc = total
            };

        private static SaldoFacturaCompra Pend(int entry, decimal total = 100m) =>
            new(entry, total, 0m, 0m, total, EstadoPagoFactura.Pendiente);

        private static SaldoFacturaCompra Parc(int entry, decimal total = 100m, decimal pagado = 40m) =>
            new(entry, total, 0m, pagado, total - pagado, EstadoPagoFactura.Parcial);

        private static SaldoFacturaCompra Paga(int entry, decimal total = 100m) =>
            new(entry, total, 0m, total, 0m, EstadoPagoFactura.Pagada);

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor>();
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        private static FiltrosReporte ConCorte(string corte, Action<Dictionary<string, FiltroValor>>? ajustar = null) =>
            Filtros(d =>
            {
                d["fechaCorte"] = new FiltroValor { Valor = corte };
                ajustar?.Invoke(d);
            });

        private static readonly DateTime Dia1 = new(2026, 1, 10, 9, 30, 0);
        private static readonly DateTime Dia2 = new(2026, 1, 20, 14, 0, 0);
        private static readonly DateTime Dia3 = new(2026, 2, 1, 8, 0, 0);

        [Fact]
        public void Definicion_EsDeCobrosYPagosConSusFiltrosYColumnas()
        {
            var d = Crear(Array.Empty<FacturaCompra>()).Definicion;

            Assert.Equal("facturas-por-pagar", d.Codigo);
            Assert.Equal("Facturas por pagar", d.Nombre);
            Assert.Equal("Facturas y notas de débito de proveedores con su saldo, vencimiento y días de atraso.", d.Descripcion);
            Assert.Equal("Cobros y pagos", d.Familia);
            Assert.Equal("ReportesCobrosPagos", d.Modulo);
            Assert.Equal(new[] { "proveedor", "estadoPago", "soloVencidas", "fechaCorte" }, d.Filtros.Select(f => f.Campo).ToArray());

            var cliente = d.Filtros.Single(f => f.Campo == "proveedor");
            Assert.Equal(TipoFiltro.Socio, cliente.Tipo);
            Assert.Equal("P", cliente.SubTipo);

            var estado = d.Filtros.Single(f => f.Campo == "estadoPago");
            Assert.Equal(TipoFiltro.Lista, estado.Tipo);
            Assert.Equal("ABIERTAS", estado.ValorPorDefecto);
            Assert.Equal(new[] { "ABIERTAS", "PENDIENTE", "PARCIAL", "PAGADA", "TODAS" }, estado.Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal("Pendientes y parciales", estado.Opciones!.Single(o => o.Valor == "ABIERTAS").Titulo);

            var vencidas = d.Filtros.Single(f => f.Campo == "soloVencidas");
            Assert.Equal("N", vencidas.ValorPorDefecto);
            Assert.Equal(new[] { "N", "S" }, vencidas.Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(new[] { "No", "Sí" }, vencidas.Opciones!.Select(o => o.Titulo).ToArray());

            var corte = d.Filtros.Single(f => f.Campo == "fechaCorte");
            Assert.Equal(TipoFiltro.Fecha, corte.Tipo);
            Assert.Equal("Fecha de corte", corte.Titulo);

            Assert.Equal(
                new[]
                {
                    "fecha", "tipo", "serie", "numDoc", "codigoSn", "nombreSn", "vencimiento", "moneda",
                    "total", "acreditado", "pagado", "saldo", "estado", "diasAtraso"
                },
                d.Columnas.Select(c => c.Campo).ToArray());
            foreach (var campo in new[] { "total", "acreditado", "pagado", "saldo" })
            {
                Assert.Equal(TipoColumna.Dinero, d.Columnas.Single(c => c.Campo == campo).Tipo);
                Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == campo).Totaliza);
            }
            Assert.Equal("Notas de crédito", d.Columnas.Single(c => c.Campo == "acreditado").Titulo);
            Assert.Equal(TipoColumna.Entero, d.Columnas.Single(c => c.Campo == "diasAtraso").Tipo);
            Assert.Equal(TipoColumna.Fecha, d.Columnas.Single(c => c.Campo == "vencimiento").Tipo);
            Assert.True(d.Columnas.Single(c => c.Campo == "tipo").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "nombreSn").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "estado").Agrupable);
            Assert.Equal("Proveedor", d.Columnas.Single(c => c.Campo == "nombreSn").Titulo);
            Assert.False(d.Columnas.Single(c => c.Campo == "serie").VisiblePorDefecto);
            Assert.False(d.Columnas.Single(c => c.Campo == "codigoSn").VisiblePorDefecto);
            Assert.False(d.Columnas.Single(c => c.Campo == "moneda").VisiblePorDefecto);
        }

        [Fact]
        public async Task Ejecutar_TomaTotalesYEstadoDelServicioDeSaldos()
        {
            var r = Crear(
                new[] { Fac(1, Dia1, Dia2, total: 200m, numDoc: 77) },
                new SaldoFacturaCompra(1, 200m, 30m, 50m, 120m, EstadoPagoFactura.Parcial));

            var filas = await r.EjecutarAsync(ConCorte("2026-01-15"), 100);

            var f = Assert.Single(filas);
            Assert.Equal(Dia1, f["fecha"]);
            Assert.Equal("Factura de compra", f["tipo"]);
            Assert.Equal(1, f["serie"]);
            Assert.Equal(77, f["numDoc"]);
            Assert.Equal("P1", f["codigoSn"]);
            Assert.Equal("Proveedor P1", f["nombreSn"]);
            Assert.Equal(Dia2, f["vencimiento"]);
            Assert.Equal("QTZ", f["moneda"]);
            Assert.Equal(200m, f["total"]);
            Assert.Equal(30m, f["acreditado"]);
            Assert.Equal(50m, f["pagado"]);
            Assert.Equal(120m, f["saldo"]);
            Assert.Equal("Parcial", f["estado"]);
        }

        [Fact]
        public async Task Ejecutar_SinVencimientoUsaLaFechaDelDocumento()
        {
            var r = Crear(new[] { Fac(1, Dia1, vence: null) }, Pend(1));

            var filas = await r.EjecutarAsync(ConCorte("2026-01-31"), 100);

            Assert.Equal(Dia1, filas.Single()["vencimiento"]);
            Assert.Equal(21, filas.Single()["diasAtraso"]);
        }

        [Theory]
        [InlineData(null, new[] { 1, 2 })]
        [InlineData("ABIERTAS", new[] { 1, 2 })]
        [InlineData("PENDIENTE", new[] { 1 })]
        [InlineData("PARCIAL", new[] { 2 })]
        [InlineData("PAGADA", new[] { 3 })]
        [InlineData("TODAS", new[] { 1, 2, 3 })]
        public async Task Ejecutar_FiltroEstadoPago(string? estadoPago, int[] esperados)
        {
            var r = Crear(
                new[] { Fac(1, Dia1, Dia2), Fac(2, Dia1, Dia2), Fac(3, Dia1, Dia2) },
                Pend(1), Parc(2), Paga(3));

            var filas = await r.EjecutarAsync(
                ConCorte("2026-03-01", d => { if (estadoPago is not null) d["estadoPago"] = new FiltroValor { Valor = estadoPago }; }), 100);

            Assert.Equal(esperados, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_DiasDeAtrasoEsLaDiferenciaEntreElCorteYElVencimiento()
        {
            // Vence el 20-ene; corte el 30-ene (con hora en el vencimiento): 10 días de atraso.
            var r = Crear(new[] { Fac(1, Dia1, Dia2) }, Pend(1));

            var filas = await r.EjecutarAsync(ConCorte("2026-01-30"), 100);

            Assert.Equal(10, filas.Single()["diasAtraso"]);
        }

        [Fact]
        public async Task Ejecutar_VenceElDiaDelCorteNoEstaVencida()
        {
            var r = Crear(new[] { Fac(1, Dia1, Dia2) }, Pend(1));

            var filas = await r.EjecutarAsync(ConCorte("2026-01-20", d => d["soloVencidas"] = new FiltroValor { Valor = "S" }), 100);

            Assert.Empty(filas);
            filas = await r.EjecutarAsync(ConCorte("2026-01-20"), 100);
            Assert.Equal(0, filas.Single()["diasAtraso"]);
        }

        [Fact]
        public async Task Ejecutar_VencimientoFuturoOSaldoCeroNoTienenAtraso()
        {
            var r = Crear(
                new[] { Fac(1, Dia1, Dia3), Fac(2, Dia1, Dia1) },
                Pend(1), Paga(2));

            var filas = await r.EjecutarAsync(
                ConCorte("2026-01-25", d => d["estadoPago"] = new FiltroValor { Valor = "TODAS" }), 100);

            // Factura 2: vencida pero pagada (saldo 0); factura 1: aún no vence. Ambas con 0 días de atraso.
            Assert.Equal(2, filas.Count);
            Assert.All(filas, f => Assert.Equal(0, f["diasAtraso"]));
        }

        [Fact]
        public async Task Ejecutar_SoloVencidasDejaLasConSaldoYVencimientoAnteriorAlCorte()
        {
            var r = Crear(
                new[]
                {
                    Fac(1, Dia1, Dia2),               // vencida, con saldo
                    Fac(2, Dia1, Dia3),               // no vencida
                    Fac(3, Dia1, Dia2),               // vencida pero pagada
                    Fac(4, Dia1, Dia2)                // vencida, parcial
                },
                Pend(1), Pend(2), Paga(3), Parc(4));

            var filas = await r.EjecutarAsync(
                ConCorte("2026-01-25", d =>
                {
                    d["soloVencidas"] = new FiltroValor { Valor = "S" };
                    d["estadoPago"] = new FiltroValor { Valor = "TODAS" };
                }), 100);

            Assert.Equal(new[] { 1, 4 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_DiasDeAtrasoConSoloVencidas()
        {
            var r = Crear(new[] { Fac(1, Dia1, Dia2) }, Pend(1));

            var filas = await r.EjecutarAsync(
                ConCorte("2026-02-19", d => d["soloVencidas"] = new FiltroValor { Valor = "S" }), 100);

            Assert.Equal(30, filas.Single()["diasAtraso"]);
        }

        [Fact]
        public async Task Ejecutar_LaFechaDeCorteExcluyeLasFacturasPosterioresEIncluyeElDiaCompleto()
        {
            var r = Crear(
                new[] { Fac(1, Dia1), Fac(2, new DateTime(2026, 1, 20, 23, 59, 0)), Fac(3, new DateTime(2026, 1, 21, 0, 0, 0)) },
                Pend(1), Pend(2), Pend(3));

            var filas = await r.EjecutarAsync(ConCorte("2026-01-20"), 100);

            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_SinFechaDeCorteUsaHoy()
        {
            var hoy = DateTime.Today;
            var r = Crear(
                new[]
                {
                    Fac(1, hoy.AddDays(-30), hoy.AddDays(-10)),
                    Fac(2, hoy.AddHours(8), hoy.AddDays(5)),
                    Fac(3, hoy.AddDays(1), hoy.AddDays(10))     // mañana: posterior al corte
                },
                Pend(1), Pend(2), Pend(3));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
            Assert.Equal(10, filas[0]["diasAtraso"]);
            Assert.Equal(0, filas[1]["diasAtraso"]);
        }

        [Fact]
        public async Task Ejecutar_IncluyeNotasDeDebitoYExcluyeCanceladasYOtrosSubtipos()
        {
            var r = Crear(
                new[]
                {
                    Fac(1, Dia1, Dia2),
                    Fac(2, Dia1, Dia2, sub: "DN"),
                    Fac(3, Dia1, Dia2, cancelado: "S"),
                    Fac(4, Dia1, Dia2, sub: "NC")
                },
                Pend(1), Pend(2), Pend(3), Pend(4));

            var filas = await r.EjecutarAsync(ConCorte("2026-03-01"), 100);

            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
            Assert.Equal("Factura de compra", filas[0]["tipo"]);
            Assert.Equal("Nota de débito de compra", filas[1]["tipo"]);
        }

        [Fact]
        public async Task Ejecutar_FiltroProveedor()
        {
            var r = Crear(new[] { Fac(1, Dia1, cliente: "P1"), Fac(2, Dia1, cliente: "P2") }, Pend(1), Pend(2));

            var filas = await r.EjecutarAsync(ConCorte("2026-03-01", d => d["proveedor"] = new FiltroValor { Valor = "P2" }), 100);

            Assert.Equal("P2", filas.Single()["codigoSn"]);
        }

        [Fact]
        public async Task Ejecutar_UnaFacturaAusenteDelServicioSeOmite()
        {
            var r = Crear(new[] { Fac(1, Dia1, Dia2), Fac(2, Dia1, Dia2) }, Pend(2));

            var filas = await r.EjecutarAsync(ConCorte("2026-03-01"), 100);

            Assert.Equal(2, filas.Single()["numDoc"]);
        }

        [Fact]
        public async Task Ejecutar_OrdenaPorVencimientoFechaYNumero()
        {
            var r = Crear(
                new[]
                {
                    Fac(1, Dia2, Dia3, numDoc: 5),
                    Fac(2, Dia2, Dia1, numDoc: 9),
                    Fac(3, Dia1, Dia1, numDoc: 8),
                    Fac(4, Dia1, Dia1, numDoc: 3)
                },
                Pend(1), Pend(2), Pend(3), Pend(4));

            var filas = await r.EjecutarAsync(ConCorte("2026-03-01"), 100);

            Assert.Equal(new[] { 3, 8, 9, 5 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_LimiteSeAplicaDespuesDeFiltrarPorSaldo()
        {
            // Las 3 primeras por orden están pagadas: con el límite aplicado antes del filtro no saldría nada.
            var facturas = new[]
            {
                Fac(1, Dia1, Dia1.AddDays(1)), Fac(2, Dia1, Dia1.AddDays(2)), Fac(3, Dia1, Dia1.AddDays(3)),
                Fac(4, Dia1, Dia1.AddDays(4)), Fac(5, Dia1, Dia1.AddDays(5))
            };
            var r = Crear(facturas, Paga(1), Paga(2), Paga(3), Pend(4), Pend(5));

            var filas = await r.EjecutarAsync(ConCorte("2026-03-01"), 2);

            Assert.Equal(new[] { 4, 5 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
            filas = await r.EjecutarAsync(ConCorte("2026-03-01"), 1);
            Assert.Equal(new[] { 4 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_PideLosSaldosEnLotesDeMil()
        {
            var facturas = Enumerable.Range(1, 2500).Select(i => Fac(i, Dia1, Dia2)).ToArray();
            var saldos = Enumerable.Range(1, 2500).Select(i => Pend(i)).ToArray();
            var r = Crear(facturas, saldos);

            var filas = await r.EjecutarAsync(ConCorte("2026-03-01"), 10000);

            Assert.Equal(2500, filas.Count);
            Assert.Equal(new[] { 1000, 1000, 500 }, _tamanosLote.ToArray());
            _saldos.Verify(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Exactly(3));
        }

        [Fact]
        public async Task Ejecutar_SinFacturasNoLlamaAlServicio()
        {
            var r = Crear(Array.Empty<FacturaCompra>());

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Empty(filas);
            _saldos.Verify(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
        }
    }
}
