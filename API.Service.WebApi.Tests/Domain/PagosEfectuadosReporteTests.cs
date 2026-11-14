using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PagosEfectuadosReporteTests
    {
        private readonly Mock<IRepositorioGenerico<PagoEfectuado, int>> _repoPagos = new();
        private readonly Mock<IRepositorioGenerico<PagoEfectuadoMedio, (int Entry, int NoLinea)>> _repoMedios = new();
        private readonly Mock<IRepositorioGenerico<PagoEfectuadoFactura, (int Entry, int NoLinea)>> _repoAplicaciones = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoFacturas = new();

        private PagosEfectuadosReporte Crear(PagoEfectuado[] pagos, PagoEfectuadoMedio[] medios, PagoEfectuadoFactura[]? aplicaciones = null, FacturaCompra[]? facturas = null)
        {
            _repoPagos.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(pagos.AsAsyncQueryable());
            _repoMedios.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(medios.AsAsyncQueryable());
            _repoAplicaciones.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((aplicaciones ?? Array.Empty<PagoEfectuadoFactura>()).AsAsyncQueryable());
            _repoFacturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((facturas ?? Array.Empty<FacturaCompra>()).AsAsyncQueryable());
            return new PagosEfectuadosReporte(_repoPagos.Object, _repoMedios.Object, _repoAplicaciones.Object, _repoFacturas.Object);
        }

        private static PagoEfectuado Pago(int entry, DateTime fecha, string proveedor = "P1", string cancelado = "N", int? numDoc = null) =>
            new()
            {
                Entry = entry, NumDoc = numDoc ?? entry, Serie = 3, Cancelado = cancelado, FechaDoc = fecha,
                CodigoSn = proveedor, NombreSn = "Proveedor " + proveedor
            };

        private static PagoEfectuadoMedio Medio(int entry, int linea, string forma = "EFE", decimal monto = 100m, string? referencia = null,
            string? banco = null, string? nombreForma = "Efectivo") =>
            new()
            {
                Entry = entry, NoLinea = linea, CodFormaPago = forma, Monto = monto, Referencia = referencia, Banco = banco,
                CodFormaPagoNavigation = nombreForma is null ? null : new FormaPago { Codigo = forma, Nombre = nombreForma }
            };

        private static PagoEfectuadoFactura Aplica(int entry, int linea, int facturaEntry) =>
            new() { Entry = entry, NoLinea = linea, FacturaCompraEntry = facturaEntry, MontoAplicado = 10m };

        private static FacturaCompra Fac(int entry, int numDoc) => new() { Entry = entry, NumDoc = numDoc, Serie = 1, DocSubType = "--" };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor>();
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        private static readonly DateTime Dia1 = new(2026, 1, 10, 9, 30, 0);
        private static readonly DateTime Dia2 = new(2026, 1, 31, 14, 0, 0);
        private static readonly DateTime Dia3 = new(2026, 2, 1, 8, 0, 0);

        [Fact]
        public void Definicion_EsDeCobrosYPagosConSusFiltrosYColumnas()
        {
            var d = Crear(Array.Empty<PagoEfectuado>(), Array.Empty<PagoEfectuadoMedio>()).Definicion;

            Assert.Equal("pagos-efectuados", d.Codigo);
            Assert.Equal("Pagos efectuados", d.Nombre);
            Assert.Equal("Pagos a proveedores por forma de pago, con las facturas de compra a las que se aplicaron.", d.Descripcion);
            Assert.Equal("Cobros y pagos", d.Familia);
            Assert.Equal("ReportesCobrosPagos", d.Modulo);
            Assert.Equal(new[] { "fechas", "proveedor", "formaPago" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal("P", d.Filtros.Single(f => f.Campo == "proveedor").SubTipo);
            Assert.Equal(TipoFiltro.Socio, d.Filtros.Single(f => f.Campo == "proveedor").Tipo);
            var forma = d.Filtros.Single(f => f.Campo == "formaPago");
            Assert.Equal(TipoFiltro.Texto, forma.Tipo);
            Assert.Equal("Forma de pago (código)", forma.Titulo);
            Assert.Equal(
                new[] { "fecha", "serie", "numDoc", "codigoSn", "nombreSn", "formaPago", "referencia", "banco", "monto", "facturasAplicadas" },
                d.Columnas.Select(c => c.Campo).ToArray());
            Assert.DoesNotContain(d.Columnas, c => c.Campo == "moneda");
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "monto").Totaliza);
            Assert.Equal(TipoColumna.Dinero, d.Columnas.Single(c => c.Campo == "monto").Tipo);
            Assert.Equal(TipoColumna.Texto, d.Columnas.Single(c => c.Campo == "facturasAplicadas").Tipo);
            Assert.Equal("Proveedor", d.Columnas.Single(c => c.Campo == "nombreSn").Titulo);
            Assert.Equal("No.", d.Columnas.Single(c => c.Campo == "numDoc").Titulo);
            Assert.True(d.Columnas.Single(c => c.Campo == "fecha").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "nombreSn").Agrupable);
            Assert.True(d.Columnas.Single(c => c.Campo == "formaPago").Agrupable);
            Assert.False(d.Columnas.Single(c => c.Campo == "serie").VisiblePorDefecto);
            Assert.False(d.Columnas.Single(c => c.Campo == "codigoSn").VisiblePorDefecto);
            Assert.False(d.Columnas.Single(c => c.Campo == "banco").VisiblePorDefecto);
        }

        [Fact]
        public async Task Ejecutar_UnaFilaPorMedioConSusDatos()
        {
            var r = Crear(
                new[] { Pago(1, Dia1, numDoc: 501) },
                new[]
                {
                    Medio(1, 1, "EFE", 60m, nombreForma: "Efectivo"),
                    Medio(1, 2, "CHQ", 40m, referencia: "1234", banco: "Banco Uno", nombreForma: "Cheque")
                });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(2, filas.Count);
            Assert.Equal(Dia1, filas[0]["fecha"]);
            Assert.Equal(3, filas[0]["serie"]);
            Assert.Equal(501, filas[0]["numDoc"]);
            Assert.Equal("P1", filas[0]["codigoSn"]);
            Assert.Equal("Proveedor P1", filas[0]["nombreSn"]);
            Assert.Equal("Efectivo", filas[0]["formaPago"]);
            Assert.Equal(60m, filas[0]["monto"]);
            Assert.Null(filas[0]["referencia"]);
            Assert.Equal("Cheque", filas[1]["formaPago"]);
            Assert.Equal(40m, filas[1]["monto"]);
            Assert.Equal("1234", filas[1]["referencia"]);
            Assert.Equal("Banco Uno", filas[1]["banco"]);
        }

        [Fact]
        public async Task Ejecutar_FormaPagoSinNombreResolubleMuestraElCodigo()
        {
            var r = Crear(new[] { Pago(1, Dia1) }, new[] { Medio(1, 1, "XYZ", nombreForma: null) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal("XYZ", filas.Single()["formaPago"]);
        }

        [Fact]
        public async Task Ejecutar_FacturasAplicadasOrdenadasPorNumeroYSeRepitenPorMedio()
        {
            var r = Crear(
                new[] { Pago(1, Dia1), Pago(2, Dia1) },
                new[] { Medio(1, 1, "EFE"), Medio(1, 2, "CHQ", nombreForma: "Cheque"), Medio(2, 1, "EFE") },
                new[] { Aplica(1, 1, 30), Aplica(1, 2, 10), Aplica(1, 3, 20), Aplica(2, 1, 20) },
                new[] { Fac(10, 12), Fac(20, 20), Fac(30, 15), Fac(40, 99) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(new[] { "12, 15, 20", "12, 15, 20", "20" }, filas.Select(f => (string)f["facturasAplicadas"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_FacturasAplicadasSeOrdenanNumericamenteNoComoTexto()
        {
            var r = Crear(
                new[] { Pago(1, Dia1) }, new[] { Medio(1, 1) },
                new[] { Aplica(1, 1, 1), Aplica(1, 2, 2) },
                new[] { Fac(1, 100), Fac(2, 20) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal("20, 100", filas.Single()["facturasAplicadas"]);
        }

        [Fact]
        public async Task Ejecutar_PagoSinFacturasAplicadas_TextoVacio()
        {
            var r = Crear(new[] { Pago(1, Dia1) }, new[] { Medio(1, 1) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal("", filas.Single()["facturasAplicadas"]);
        }

        [Fact]
        public async Task Ejecutar_FacturaAplicadaInexistenteSeOmite()
        {
            var r = Crear(new[] { Pago(1, Dia1) }, new[] { Medio(1, 1) }, new[] { Aplica(1, 1, 999), Aplica(1, 2, 10) }, new[] { Fac(10, 12) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal("12", filas.Single()["facturasAplicadas"]);
        }

        [Fact]
        public async Task Ejecutar_LasFacturasSeResuelvenConUnaSolaConsultaDeFacturas()
        {
            var r = Crear(
                new[] { Pago(1, Dia1), Pago(2, Dia2) }, new[] { Medio(1, 1), Medio(2, 1) },
                new[] { Aplica(1, 1, 10), Aplica(2, 1, 11) }, new[] { Fac(10, 1), Fac(11, 2) });

            await r.EjecutarAsync(Filtros(), 100);

            _repoFacturas.Verify(x => x.ObtenerTodoAsync(), Times.Once);
            _repoAplicaciones.Verify(x => x.ObtenerTodoAsync(), Times.Once);
        }

        [Fact]
        public async Task Ejecutar_MuchosPagos_ResuelveFacturasPorLotes()
        {
            var pagos = Enumerable.Range(1, 2500).Select(i => Pago(i, Dia1)).ToArray();
            var medios = Enumerable.Range(1, 2500).Select(i => Medio(i, 1)).ToArray();
            var aplicaciones = Enumerable.Range(1, 2500).Select(i => Aplica(i, 1, i)).ToArray();
            var facturas = Enumerable.Range(1, 2500).Select(i => Fac(i, 10000 + i)).ToArray();
            var r = Crear(pagos, medios, aplicaciones, facturas);

            var filas = await r.EjecutarAsync(Filtros(), 10000);

            Assert.Equal(2500, filas.Count);
            Assert.All(filas, f => Assert.Equal((10000 + (int)f["numDoc"]!).ToString(), f["facturasAplicadas"]));
        }

        [Fact]
        public async Task Ejecutar_ExcluyeLosPagosCancelados()
        {
            var r = Crear(
                new[] { Pago(1, Dia1), Pago(2, Dia1, cancelado: "S") },
                new[] { Medio(1, 1), Medio(2, 1) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Single(filas);
            Assert.Equal(1, filas[0]["numDoc"]);
        }

        [Fact]
        public async Task Ejecutar_MediosSinPagoSeOmiten()
        {
            var r = Crear(new[] { Pago(1, Dia1) }, new[] { Medio(1, 1), Medio(77, 1) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Single(filas);
        }

        [Fact]
        public async Task Ejecutar_FiltroProveedor()
        {
            var r = Crear(new[] { Pago(1, Dia1, "P1"), Pago(2, Dia1, "P2") }, new[] { Medio(1, 1), Medio(2, 1) });

            var filas = await r.EjecutarAsync(Filtros(d => d["proveedor"] = new FiltroValor { Valor = "P2" }), 100);

            Assert.Equal("P2", filas.Single()["codigoSn"]);
        }

        [Fact]
        public async Task Ejecutar_FiltroFormaPago_FiltraPorMedioNoPorPago()
        {
            var r = Crear(
                new[] { Pago(1, Dia1), Pago(2, Dia1) },
                new[] { Medio(1, 1, "EFE"), Medio(1, 2, "CHQ", nombreForma: "Cheque"), Medio(2, 1, "EFE") });

            var filas = await r.EjecutarAsync(Filtros(d => d["formaPago"] = new FiltroValor { Valor = "CHQ" }), 100);

            Assert.Equal("Cheque", filas.Single()["formaPago"]);
            Assert.Equal(1, filas.Single()["numDoc"]);
        }

        [Fact]
        public async Task Ejecutar_RangoDeFechas_EsInclusivoEnAmbosExtremos()
        {
            var r = Crear(new[] { Pago(1, Dia1), Pago(2, Dia2), Pago(3, Dia3) }, new[] { Medio(1, 1), Medio(2, 1), Medio(3, 1) });

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10", Hasta = "2026-01-31" }), 100);

            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_OrdenNaturalYLimiteSobreLasFilasPorMedio()
        {
            var r = Crear(
                new[] { Pago(2, Dia2), Pago(1, Dia1) },
                new[] { Medio(2, 1, monto: 4m), Medio(1, 2, monto: 2m), Medio(1, 1, monto: 1m), Medio(1, 3, monto: 3m) });

            var todas = await r.EjecutarAsync(Filtros(), 100);
            var limitadas = await r.EjecutarAsync(Filtros(), 3);

            Assert.Equal(new[] { 1m, 2m, 3m, 4m }, todas.Select(f => (decimal)f["monto"]!).ToArray());
            Assert.Equal(new[] { 1m, 2m, 3m }, limitadas.Select(f => (decimal)f["monto"]!).ToArray());
        }
    }
}
