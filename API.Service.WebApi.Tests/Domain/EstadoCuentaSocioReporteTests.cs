using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Reportes;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class EstadoCuentaSocioReporteTests
    {
        private readonly Mock<IEstadoCuentaService> _servicio = new();

        private EstadoCuentaSocioReporte Crear() => new(_servicio.Object);

        private static EstadoCuenta Estado(DateTime desde, DateTime hasta, decimal saldoInicial, params MovimientoCuenta[] movimientos) =>
            new(new SocioNegocio { Codigo = "C1", Nombre = "Cliente 1" }, desde, hasta, saldoInicial, movimientos,
                movimientos.LastOrDefault()?.SaldoAcumulado ?? saldoInicial, 0m, 0m);

        private static FiltrosReporte Filtros(string? desde = null, string? hasta = null, string socio = "C1")
        {
            var d = new Dictionary<string, FiltroValor> { ["socio"] = new FiltroValor { Valor = socio } };
            if (desde is not null || hasta is not null) d["fechas"] = new FiltroValor { Desde = desde, Hasta = hasta };
            return new FiltrosReporte(d);
        }

        private static readonly DateTime Ene1 = new(2026, 1, 1);
        private static readonly DateTime Ene31 = new(2026, 1, 31);

        [Fact]
        public void Definicion_EsDeCobrosYPagosConSusFiltrosYColumnas()
        {
            var d = Crear().Definicion;

            Assert.Equal("estado-cuenta-socio", d.Codigo);
            Assert.Equal("Estado de cuenta de un socio", d.Nombre);
            Assert.Equal("Cargos, abonos y saldo de un cliente o proveedor.", d.Descripcion);
            Assert.Equal("Cobros y pagos", d.Familia);
            Assert.Equal("ReportesCobrosPagos", d.Modulo);
            Assert.Equal(new[] { "socio", "fechas" }, d.Filtros.Select(f => f.Campo).ToArray());

            var socio = d.Filtros.Single(f => f.Campo == "socio");
            Assert.Equal(TipoFiltro.Socio, socio.Tipo);
            Assert.Null(socio.SubTipo);
            Assert.True(socio.Obligatorio);
            Assert.Equal(TipoFiltro.RangoFechas, d.Filtros.Single(f => f.Campo == "fechas").Tipo);
            Assert.False(d.Filtros.Single(f => f.Campo == "fechas").Obligatorio);

            Assert.Equal(new[] { "fecha", "tipo", "numDoc", "descripcion", "cargo", "abono", "saldo" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal(TipoColumna.Fecha, d.Columnas.Single(c => c.Campo == "fecha").Tipo);
            Assert.Equal(TipoColumna.Texto, d.Columnas.Single(c => c.Campo == "tipo").Tipo);
            Assert.True(d.Columnas.Single(c => c.Campo == "tipo").Agrupable);
            Assert.Equal(TipoColumna.Entero, d.Columnas.Single(c => c.Campo == "numDoc").Tipo);
            Assert.Equal("No.", d.Columnas.Single(c => c.Campo == "numDoc").Titulo);
            Assert.Equal(TipoColumna.Texto, d.Columnas.Single(c => c.Campo == "descripcion").Tipo);
            foreach (var campo in new[] { "cargo", "abono" })
            {
                Assert.Equal(TipoColumna.Dinero, d.Columnas.Single(c => c.Campo == campo).Tipo);
                Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == campo).Totaliza);
            }
            var saldo = d.Columnas.Single(c => c.Campo == "saldo");
            Assert.Equal(TipoColumna.Dinero, saldo.Tipo);
            Assert.Equal("Saldo", saldo.Titulo);
            Assert.Equal(Totalizacion.Ninguna, saldo.Totaliza);
            Assert.DoesNotContain(d.Columnas, c => c.Campo == "moneda");
        }

        [Fact]
        public async Task Ejecutar_PrimeraFilaEsElSaldoInicialConLaFechaDesdeDelEstado()
        {
            _servicio.Setup(s => s.ObtenerAsync("C1", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Estado(Ene1, Ene31, 250m));

            var filas = await Crear().EjecutarAsync(Filtros("2026-01-01", "2026-01-31"), 100);

            var f = Assert.Single(filas);
            Assert.Equal(Ene1, f["fecha"]);
            Assert.Equal("Saldo inicial", f["tipo"]);
            Assert.Null(f["numDoc"]);
            Assert.Equal("", f["descripcion"]);
            Assert.Equal(0m, f["cargo"]);
            Assert.Equal(0m, f["abono"]);
            Assert.Equal(250m, f["saldo"]);
        }

        [Fact]
        public async Task Ejecutar_UnRenglonPorMovimientoConSuSaldoAcumulado()
        {
            _servicio.Setup(s => s.ObtenerAsync("C1", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Estado(Ene1, Ene31, 100m,
                    new MovimientoCuenta(new DateTime(2026, 1, 5), "Factura", 12, "Factura 1-12", 300m, 0m, 400m, false),
                    new MovimientoCuenta(new DateTime(2026, 1, 20), "Pago recibido", 7, "Pago 7", 0m, 150m, 250m, false)));

            var filas = await Crear().EjecutarAsync(Filtros("2026-01-01", "2026-01-31"), 100);

            Assert.Equal(3, filas.Count);
            Assert.Equal("Saldo inicial", filas[0]["tipo"]);
            Assert.Equal(100m, filas[0]["saldo"]);

            Assert.Equal(new DateTime(2026, 1, 5), filas[1]["fecha"]);
            Assert.Equal("Factura", filas[1]["tipo"]);
            Assert.Equal(12, filas[1]["numDoc"]);
            Assert.Equal("Factura 1-12", filas[1]["descripcion"]);
            Assert.Equal(300m, filas[1]["cargo"]);
            Assert.Equal(0m, filas[1]["abono"]);
            Assert.Equal(400m, filas[1]["saldo"]);

            Assert.Equal("Pago recibido", filas[2]["tipo"]);
            Assert.Equal(7, filas[2]["numDoc"]);
            Assert.Equal(0m, filas[2]["cargo"]);
            Assert.Equal(150m, filas[2]["abono"]);
            Assert.Equal(250m, filas[2]["saldo"]);
        }

        [Fact]
        public async Task Ejecutar_LosInformativosLlevanCargoYAbonoEnCeroYConservanElSaldo()
        {
            _servicio.Setup(s => s.ObtenerAsync("C1", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Estado(Ene1, Ene31, 100m,
                    new MovimientoCuenta(new DateTime(2026, 1, 9), "Nota de crédito", 3, "NC sin factura base", 0m, 0m, 100m, true)));

            var filas = await Crear().EjecutarAsync(Filtros("2026-01-01", "2026-01-31"), 100);

            Assert.Equal(2, filas.Count);
            Assert.Equal("Nota de crédito", filas[1]["tipo"]);
            Assert.Equal(0m, filas[1]["cargo"]);
            Assert.Equal(0m, filas[1]["abono"]);
            Assert.Equal(100m, filas[1]["saldo"]);
        }

        [Fact]
        public async Task Ejecutar_PasaAlServicioElSocioYLasFechasDesdeYHastaInclusivas()
        {
            _servicio.Setup(s => s.ObtenerAsync("C1", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Estado(Ene1, Ene31, 0m));

            await Crear().EjecutarAsync(Filtros("2026-01-01", "2026-01-31"), 100);

            _servicio.Verify(s => s.ObtenerAsync("C1", new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)), Times.Once);
        }

        [Fact]
        public async Task Ejecutar_SinFechasPasaNulosAlServicio()
        {
            _servicio.Setup(s => s.ObtenerAsync("C1", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Estado(Ene1, Ene31, 0m));

            await Crear().EjecutarAsync(Filtros(), 100);

            _servicio.Verify(s => s.ObtenerAsync("C1", null, null), Times.Once);
        }

        [Fact]
        public async Task Ejecutar_SoloDesdePasaHastaNulo()
        {
            _servicio.Setup(s => s.ObtenerAsync("C1", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Estado(Ene1, Ene31, 0m));

            await Crear().EjecutarAsync(Filtros(desde: "2026-01-01"), 100);

            _servicio.Verify(s => s.ObtenerAsync("C1", new DateTime(2026, 1, 1), null), Times.Once);
        }

        [Fact]
        public async Task Ejecutar_ErrorDelServicioSeRelanzaComoReporteInvalidoConElMismoMensaje()
        {
            _servicio.Setup(s => s.ObtenerAsync("X9", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ThrowsAsync(new Exception("El socio de negocio no existe."));

            var ex = await Assert.ThrowsAsync<ReporteInvalidoException>(() =>
                Crear().EjecutarAsync(Filtros(socio: "X9"), 100));

            Assert.Equal("El socio de negocio no existe.", ex.Message);
        }

        [Fact]
        public async Task Ejecutar_FechaInicialPosteriorALaFinalSeRelanzaConElMensajeDelServicio()
        {
            _servicio.Setup(s => s.ObtenerAsync("C1", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ThrowsAsync(new Exception("La fecha inicial no puede ser posterior a la final."));

            var ex = await Assert.ThrowsAsync<ReporteInvalidoException>(() =>
                Crear().EjecutarAsync(Filtros("2026-02-01", "2026-01-01"), 100));

            Assert.Equal("La fecha inicial no puede ser posterior a la final.", ex.Message);
        }

        [Fact]
        public async Task Ejecutar_RespetaElLimiteIncluyendoElSaldoInicial()
        {
            var movs = Enumerable.Range(1, 5)
                .Select(i => new MovimientoCuenta(new DateTime(2026, 1, i), "Factura", i, "F" + i, 10m, 0m, 10m * i, false))
                .ToArray();
            _servicio.Setup(s => s.ObtenerAsync("C1", It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(Estado(Ene1, Ene31, 0m, movs));

            var filas = await Crear().EjecutarAsync(Filtros(), 3);

            Assert.Equal(3, filas.Count);
            Assert.Equal("Saldo inicial", filas[0]["tipo"]);
            Assert.Equal(1, filas[1]["numDoc"]);
            Assert.Equal(2, filas[2]["numDoc"]);
        }
    }
}
