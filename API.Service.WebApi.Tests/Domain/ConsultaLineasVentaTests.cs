using API.Domain.Core.Reportes.Compartido;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ConsultaLineasVentaTests
    {
        private static readonly DateTime Enero10 = new(2026, 1, 10);

        private readonly Mock<IRepositorioGenerico<Factura, int>> _facturas = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _detalles = new();
        private readonly Mock<IRepositorioGenerico<MovimientoInventario, int>> _movimientos = new();

        private ConsultaLineasVenta Crear(IEnumerable<Factura> facturas, IEnumerable<FacturaDetalle> detalles, IEnumerable<MovimientoInventario>? movimientos = null)
        {
            _facturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            _detalles.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(detalles.AsAsyncQueryable());
            _movimientos.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync((movimientos ?? Array.Empty<MovimientoInventario>()).AsAsyncQueryable());
            return new ConsultaLineasVenta(_facturas.Object, _detalles.Object, _movimientos.Object);
        }

        private static Factura Fac(int entry, DateTime? fecha = null, string cancelado = "N", string subTipo = "--", string sn = "C001",
            string nombreSn = "Cliente Uno", string moneda = "GTQ", int serie = 1, int numDoc = 0) =>
            new()
            {
                Entry = entry, NumDoc = numDoc == 0 ? entry * 10 : numDoc, Serie = serie, Cancelado = cancelado, DocSubType = subTipo,
                FechaDoc = fecha ?? Enero10, CodigoSn = sn, NombreSn = nombreSn, MonedaDoc = moneda
            };

        private static FacturaDetalle Lin(int entry, int noLinea, string? art = "A1", decimal cantidad = 2m, decimal? cantidadBase = null,
            decimal total = 112m, decimal impuesto = 12m, string? almacen = "01", int? baseTipo = null, int? baseEntry = null,
            int? baseLinea = null, string? grupo = "Lubricantes") =>
            new()
            {
                Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cantidad, CantidadBase = cantidadBase ?? cantidad,
                TotalLinea = total, Impuesto = impuesto, CodAlmacen = almacen, PrctjeDesc = 0m,
                BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea,
                CodArticuloNavigation = art is null ? null : new Articulo
                {
                    Codigo = art, Nombre = "Artículo " + art,
                    CodigoGrupoNavigation = grupo is null ? null : new GrupoArticulo { Codigo = 100, Nombre = grupo }
                }
            };

        private static MovimientoInventario Mov(string tipoDoc, int docEntry, int docLinea, decimal sale = 0m, decimal entra = 0m, decimal valor = 0m) =>
            new() { TipoDoc = tipoDoc, DocEntry = docEntry, DocLinea = docLinea, CodArticulo = "A1", CodAlmacen = "01",
                    CantidadSale = sale, CantidadEntra = entra, ValorMovimiento = valor };

        private static FiltroLineasVenta SinFiltro => new(null, null, null, null, null, null);

        [Fact]
        public async Task FacturaDirecta_VentaNetaSinIvaYCostoDelMovimiento6()
        {
            var c = Crear(new[] { Fac(1, numDoc: 501) }, new[] { Lin(1, 1) }, new[] { Mov("6", 1, 1, sale: 2m, valor: -60m) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(1, l.DocEntry);
            Assert.Equal(1, l.NoLinea);
            Assert.Equal(501, l.NumDoc);
            Assert.Equal("Factura", l.Tipo);
            Assert.Equal("C001", l.CodigoSn);
            Assert.Equal("Cliente Uno", l.NombreSn);
            Assert.Equal("GTQ", l.Moneda);
            Assert.Equal("A1", l.CodArticulo);
            Assert.Equal("Artículo A1", l.NombreArticulo);
            Assert.Equal("Lubricantes", l.Grupo);
            Assert.Equal("01", l.CodAlmacen);
            Assert.Equal(2m, l.CantidadBase);
            Assert.Equal(100m, l.VentaNeta);
            Assert.Equal(60m, l.Costo);
        }

        [Fact]
        public async Task CantidadBase_TomaCantidadBaseYSiEsNulaLaCantidad()
        {
            var sinBase = Lin(1, 2, cantidad: 3m);
            sinBase.CantidadBase = null;
            var conBase = Lin(1, 1, cantidad: 2m, cantidadBase: 24m);
            var c = Crear(new[] { Fac(1) }, new[] { conBase, sinBase });

            var r = await c.ObtenerAsync(SinFiltro);

            Assert.Equal(24m, r[0].CantidadBase);
            Assert.Equal(3m, r[1].CantidadBase);
        }

        [Fact]
        public async Task LineaConBaseEntrega_ElCostoSaleDelMovimiento5_IgnoraElMovimiento6()
        {
            var c = Crear(new[] { Fac(1) },
                new[] { Lin(1, 1, baseTipo: 5, baseEntry: 77, baseLinea: 3) },
                new[]
                {
                    Mov("5", 77, 3, sale: 2m, valor: -50m),
                    Mov("6", 1, 1, sale: 2m, valor: -999m),   // no debe contarse
                    Mov("5", 77, 1, sale: 2m, valor: -888m)   // otra línea de la entrega
                });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(50m, l.Costo);
        }

        [Fact]
        public async Task LineaConBaseOrdenServicio_ElCostoSaleDelMovimiento70()
        {
            var c = Crear(new[] { Fac(1) },
                new[] { Lin(1, 1, baseTipo: 70, baseEntry: 9, baseLinea: 2) },
                new[]
                {
                    Mov("70", 9, 2, sale: 2m, valor: -44m),
                    Mov("6", 1, 1, sale: 2m, valor: -999m),
                    Mov("5", 9, 2, sale: 2m, valor: -777m)    // mismo entry pero otro tipo de documento
                });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(44m, l.Costo);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        public async Task LineaConBaseCotizacionOPedido_ElCostoSaleDelMovimiento6DeLaFactura(int baseTipo)
        {
            var c = Crear(new[] { Fac(1) },
                new[] { Lin(1, 1, baseTipo: baseTipo, baseEntry: 5, baseLinea: 1) },
                new[] { Mov("6", 1, 1, sale: 2m, valor: -60m), Mov("5", 5, 1, sale: 2m, valor: -999m) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(60m, l.Costo);
        }

        [Fact]
        public async Task FacturacionParcialDeUnaEntrega_CostoProporcionalALaCantidadFacturada()
        {
            var c = Crear(new[] { Fac(1) },
                new[] { Lin(1, 1, cantidad: 4m, baseTipo: 5, baseEntry: 20, baseLinea: 1) },
                new[] { Mov("5", 20, 1, sale: 10m, valor: -100m) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(40m, l.Costo);
        }

        [Fact]
        public async Task MovimientoRevertido_DenominadorCero_CostoCero()
        {
            var c = Crear(new[] { Fac(1) }, new[] { Lin(1, 1) },
                new[] { Mov("6", 1, 1, sale: 2m, valor: -60m), Mov("6", 1, 1, entra: 2m, valor: 60m) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(0m, l.Costo);
            Assert.Equal(100m, l.VentaNeta);
        }

        [Fact]
        public async Task SinMovimientos_Servicio_CostoCeroYVentaNetaIntacta()
        {
            var c = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, total: 224m, impuesto: 24m) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(0m, l.Costo);
            Assert.Equal(200m, l.VentaNeta);
        }

        [Fact]
        public async Task TotalLineaOImpuestoNulos_SeTratanComoCero()
        {
            var l1 = Lin(1, 1);
            l1.TotalLinea = null;
            l1.Impuesto = null;
            var c = Crear(new[] { Fac(1) }, new[] { l1 });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(0m, l.VentaNeta);
        }

        [Fact]
        public async Task SumaVariosMovimientosDeLaMismaLinea()
        {
            var c = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, cantidad: 3m) },
                new[] { Mov("6", 1, 1, sale: 1m, valor: -10m), Mov("6", 1, 1, sale: 2m, valor: -30m) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(40m, l.Costo);   // 3 × (40 / 3)
        }

        [Fact]
        public async Task VentaNetaYCosto_SeRedondeanASeisDecimales()
        {
            var c = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, cantidad: 1m, total: 10.1234567m, impuesto: 0m) },
                new[] { Mov("6", 1, 1, sale: 3m, valor: -10m) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(10.123457m, l.VentaNeta);
            Assert.Equal(3.333333m, l.Costo);
        }

        [Fact]
        public async Task ExcluyeFacturasCanceladas()
        {
            var c = Crear(new[] { Fac(1, cancelado: "S"), Fac(2) }, new[] { Lin(1, 1), Lin(2, 1) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(2, l.DocEntry);
        }

        [Fact]
        public async Task NotaDeDebito_SeIncluyeConTipoNotaDeDebito_YNotaDeCreditoSeExcluye()
        {
            var c = Crear(new[] { Fac(1), Fac(2, subTipo: "ND"), Fac(3, subTipo: "NC") }, new[] { Lin(1, 1), Lin(2, 1), Lin(3, 1) });

            var r = await c.ObtenerAsync(SinFiltro);

            Assert.Equal(new[] { 1, 2 }, r.Select(x => x.DocEntry).ToArray());
            Assert.Equal("Factura", r[0].Tipo);
            Assert.Equal("Nota de débito", r[1].Tipo);
        }

        [Fact]
        public async Task ExcluyeLineasSinArticulo()
        {
            var c = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, art: null), Lin(1, 2) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(2, l.NoLinea);
        }

        [Fact]
        public async Task ExcluyeLineasDeOtrosDocumentos()
        {
            var c = Crear(new[] { Fac(1) }, new[] { Lin(1, 1), Lin(99, 1) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro));

            Assert.Equal(1, l.DocEntry);
        }

        [Fact]
        public async Task FiltroFechas_DesdeInclusivoYHastaExclusivo()
        {
            var c = Crear(
                new[] { Fac(1, new DateTime(2026, 1, 9)), Fac(2, new DateTime(2026, 1, 10)), Fac(3, new DateTime(2026, 1, 19, 23, 59, 0)), Fac(4, new DateTime(2026, 1, 20)) },
                new[] { Lin(1, 1), Lin(2, 1), Lin(3, 1), Lin(4, 1) });

            var r = await c.ObtenerAsync(new FiltroLineasVenta(new DateTime(2026, 1, 10), new DateTime(2026, 1, 20), null, null, null, null));

            Assert.Equal(new[] { 2, 3 }, r.Select(x => x.DocEntry).ToArray());
        }

        [Fact]
        public async Task FiltroCliente()
        {
            var c = Crear(new[] { Fac(1, sn: "C001"), Fac(2, sn: "C002") }, new[] { Lin(1, 1), Lin(2, 1) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro with { Cliente = "C002" }));

            Assert.Equal(2, l.DocEntry);
        }

        [Fact]
        public async Task FiltroArticulo()
        {
            var c = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, art: "A1"), Lin(1, 2, art: "A2") });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro with { Articulo = "A2" }));

            Assert.Equal("A2", l.CodArticulo);
        }

        [Fact]
        public async Task FiltroAlmacen()
        {
            var c = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, almacen: "01"), Lin(1, 2, almacen: "02") });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro with { Almacen = "02" }));

            Assert.Equal("02", l.CodAlmacen);
        }

        [Fact]
        public async Task FiltroGrupo_BuscaPorNombreContiene_YExcluyeArticulosSinGrupo()
        {
            var c = Crear(new[] { Fac(1) },
                new[] { Lin(1, 1, grupo: "Lubricantes"), Lin(1, 2, grupo: "Filtros"), Lin(1, 3, grupo: null) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro with { Grupo = "Lubri" }));

            Assert.Equal(1, l.NoLinea);
        }

        [Fact]
        public async Task LosFiltrosAcotanTambienLosMovimientosUsados()
        {
            // El filtro de artículo deja solo la línea 2; su costo sigue viniendo de su propio movimiento.
            var c = Crear(new[] { Fac(1) }, new[] { Lin(1, 1, art: "A1"), Lin(1, 2, art: "A2") },
                new[] { Mov("6", 1, 1, sale: 2m, valor: -10m), Mov("6", 1, 2, sale: 2m, valor: -70m) });

            var l = Assert.Single(await c.ObtenerAsync(SinFiltro with { Articulo = "A2" }));

            Assert.Equal(70m, l.Costo);
        }

        [Fact]
        public async Task OrdenaPorFechaDocEntryYNoLinea()
        {
            var c = Crear(
                new[] { Fac(1, new DateTime(2026, 3, 1)), Fac(2, new DateTime(2026, 1, 1)), Fac(3, new DateTime(2026, 1, 1)) },
                new[] { Lin(1, 1), Lin(3, 2), Lin(3, 1), Lin(2, 1) });

            var r = await c.ObtenerAsync(SinFiltro);

            Assert.Equal(new[] { (2, 1), (3, 1), (3, 2), (1, 1) }, r.Select(x => (x.DocEntry, x.NoLinea)).ToArray());
        }

        [Fact]
        public async Task ConMasDeMaxLineas_LanzaReporteInvalido()
        {
            Assert.Equal(200000, ConsultaLineasVenta.MaxLineas);
            var lineas = new List<FacturaDetalle>(ConsultaLineasVenta.MaxLineas + 1);
            for (var i = 1; i <= ConsultaLineasVenta.MaxLineas + 1; i++)
                lineas.Add(Lin(1, i));
            var c = Crear(new[] { Fac(1) }, lineas);

            var ex = await Assert.ThrowsAsync<ReporteInvalidoException>(() => c.ObtenerAsync(SinFiltro));

            Assert.Equal("El período tiene demasiadas líneas para este reporte. Acorta el rango de fechas.", ex.Message);
        }

        [Fact]
        public async Task ConExactamenteMaxLineas_NoLanza()
        {
            var lineas = new List<FacturaDetalle>(ConsultaLineasVenta.MaxLineas);
            for (var i = 1; i <= ConsultaLineasVenta.MaxLineas; i++)
                lineas.Add(Lin(1, i));
            var c = Crear(new[] { Fac(1) }, lineas);

            var r = await c.ObtenerAsync(SinFiltro);

            Assert.Equal(ConsultaLineasVenta.MaxLineas, r.Count);
        }

        [Fact]
        public async Task Limitado_ConMasDeMaxLineas_NoLanzaYDevuelveLasPrimerasConSusCostos()
        {
            var lineas = new List<FacturaDetalle>(ConsultaLineasVenta.MaxLineas + 1);
            for (var i = 1; i <= ConsultaLineasVenta.MaxLineas + 1; i++)
                lineas.Add(Lin(1, i));
            var movimientos = new List<MovimientoInventario>();
            for (var i = 1; i <= 10; i++)
                movimientos.Add(Mov("6", 1, i, sale: 2m, valor: -10m * i));
            var c = Crear(new[] { Fac(1) }, lineas, movimientos);

            var r = await c.ObtenerLimitadoAsync(SinFiltro, 10);

            Assert.Equal(10, r.Count);
            Assert.Equal(Enumerable.Range(1, 10), r.Select(x => x.NoLinea));
            Assert.Equal(Enumerable.Range(1, 10).Select(i => 10m * i), r.Select(x => x.Costo));
        }

        [Fact]
        public async Task SinLimite_ConMasDeMaxLineas_SigueLanzandoAunqueExistaElCaminoLimitado()
        {
            var lineas = new List<FacturaDetalle>(ConsultaLineasVenta.MaxLineas + 1);
            for (var i = 1; i <= ConsultaLineasVenta.MaxLineas + 1; i++)
                lineas.Add(Lin(1, i));
            var c = Crear(new[] { Fac(1) }, lineas);

            await Assert.ThrowsAsync<ReporteInvalidoException>(() => c.ObtenerAsync(SinFiltro));
            Assert.Equal(5, (await c.ObtenerLimitadoAsync(SinFiltro, 5)).Count);
        }

        [Fact]
        public async Task Limitado_CostoPorLosTresOrigenes_ConLoteDeMasDeMilEntries()
        {
            // 2,500 facturas de una línea cada una (3 lotes de 1,000): propia ("6"), entrega ("5") y orden de servicio ("70") por turnos.
            const int n = 2500;
            var facturas = new List<Factura>();
            var detalles = new List<FacturaDetalle>();
            var movimientos = new List<MovimientoInventario>();
            for (var i = 1; i <= n; i++)
            {
                facturas.Add(Fac(i, new DateTime(2026, 1, 1).AddMinutes(i)));
                switch (i % 3)
                {
                    case 0:
                        detalles.Add(Lin(i, 1));
                        movimientos.Add(Mov("6", i, 1, sale: 2m, valor: -2m * i));
                        break;
                    case 1:
                        detalles.Add(Lin(i, 1, baseTipo: 5, baseEntry: 100000 + i, baseLinea: 4));
                        movimientos.Add(Mov("5", 100000 + i, 4, sale: 2m, valor: -3m * i));
                        movimientos.Add(Mov("6", i, 1, sale: 2m, valor: -999m));      // no debe contarse
                        break;
                    default:
                        detalles.Add(Lin(i, 1, baseTipo: 70, baseEntry: 200000 + i, baseLinea: 2));
                        movimientos.Add(Mov("70", 200000 + i, 2, sale: 2m, valor: -4m * i));
                        movimientos.Add(Mov("5", 200000 + i, 2, sale: 2m, valor: -888m)); // otro tipo con el mismo entry
                        break;
                }
            }
            var c = Crear(facturas, detalles, movimientos);

            var r = await c.ObtenerLimitadoAsync(SinFiltro, n);

            Assert.Equal(n, r.Count);
            foreach (var l in r)
            {
                var esperado = (l.DocEntry % 3) switch { 0 => 2m * l.DocEntry, 1 => 3m * l.DocEntry, _ => 4m * l.DocEntry };
                Assert.Equal(esperado, l.Costo);
            }
        }

        [Fact]
        public async Task Limitado_RespetaElOrdenYLosFiltros()
        {
            var c = Crear(
                new[] { Fac(1, new DateTime(2026, 3, 1)), Fac(2, new DateTime(2026, 1, 1)), Fac(3, new DateTime(2026, 1, 1), cancelado: "S") },
                new[] { Lin(1, 1), Lin(2, 2), Lin(2, 1), Lin(3, 1) });

            var r = await c.ObtenerLimitadoAsync(SinFiltro, 2);

            Assert.Equal(new[] { (2, 1), (2, 2) }, r.Select(x => (x.DocEntry, x.NoLinea)).ToArray());
        }

        [Fact]
        public async Task Limitado_ConLimiteCeroDevuelveVacio()
        {
            var c = Crear(new[] { Fac(1) }, new[] { Lin(1, 1) });

            Assert.Empty(await c.ObtenerLimitadoAsync(SinFiltro, 0));
        }
    }
}
