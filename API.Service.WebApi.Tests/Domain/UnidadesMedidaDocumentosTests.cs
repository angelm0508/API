using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class UnidadesMedidaDocumentosTests
    {
        private const int Unidad = 1, Caja = 2;

        private static Mock<IUnidadMedidaService> CrearUnidadesMock()
        {
            var m = new Mock<IUnidadMedidaService>();
            m.Setup(u => u.ResolverAsync("ART-1", null))
                .ReturnsAsync(new UnidadResuelta(Unidad, "UND", 1m));
            m.Setup(u => u.ResolverAsync("ART-1", Unidad))
                .ReturnsAsync(new UnidadResuelta(Unidad, "UND", 1m));
            m.Setup(u => u.ResolverAsync("ART-1", Caja))
                .ReturnsAsync(new UnidadResuelta(Caja, "CAJA", 12m));
            return m;
        }

        private static Mock<IEjecutorTransaccion> CrearTxMock()
        {
            var tx = new Mock<IEjecutorTransaccion>();
            tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()))
                .Returns((Func<Task<int>> f) => f());
            tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>()))
                .Returns((Func<Task<bool>> f) => f());
            return tx;
        }

        [Fact]
        public async Task CotizacionDetalle_InsertarYActualizar_ResuelveUnidadYCantidadBase()
        {
            var repo = new Mock<IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)>>();
            repo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<CotizacionDetalle>().AsAsyncQueryable());
            repo.Setup(r => r.InsertarAsync(It.IsAny<CotizacionDetalle>())).ReturnsAsync((CotizacionDetalle d) => d);
            repo.Setup(r => r.ActualizarAsync(It.IsAny<(int, int)>(), It.IsAny<CotizacionDetalle>())).ReturnsAsync(true);
            var docBase = new Mock<IDocumentoBaseService>();
            var unidades = CrearUnidadesMock();

            var domain = new CotizacionDetalleDomain(repo.Object, docBase.Object, unidades.Object);

            var linea = new CotizacionDetalle { Entry = 10, CodArticulo = "ART-1", Cantidad = 3m, UomEntry = Caja };
            await domain.InsertarAsync(linea);

            Assert.Equal(Caja, linea.UomEntry);
            Assert.Equal(12m, linea.FactorUom);
            Assert.Equal(36m, linea.CantidadBase);

            var editada = new CotizacionDetalle { Entry = 10, NoLinea = 1, CodArticulo = "ART-1", Cantidad = 5m, UomEntry = null };
            await domain.ActualizarAsync(10, 1, editada);

            Assert.Equal(Unidad, editada.UomEntry);
            Assert.Equal(1m, editada.FactorUom);
            Assert.Equal(5m, editada.CantidadBase);
        }

        [Fact]
        public async Task PedidoDetalle_LineaCopiada_NoPermiteCambiarUnidadDeMedida()
        {
            var repo = new Mock<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>>();
            repo.Setup(r => r.ObtenerAsync(ValueTuple.Create(10, 1))).ReturnsAsync(new PedidoDetalle
            {
                Entry = 10, NoLinea = 1, BaseTipo = TipoDocumentoVenta.Cotizacion, BaseEntry = 1, BaseLinea = 1,
                CodArticulo = "ART-1", Cantidad = 4m, UomEntry = Caja, FactorUom = 12m, CantidadBase = 48m
            });
            var docBase = new Mock<IDocumentoBaseService>();
            var unidades = CrearUnidadesMock();

            var domain = new PedidoDetalleDomain(repo.Object, docBase.Object, unidades.Object);

            var intentoCambioUom = new PedidoDetalle
            {
                Entry = 10, NoLinea = 1, CodArticulo = "ART-1", Cantidad = 2m, UomEntry = Unidad
            };

            var ex = await Assert.ThrowsAsync<Exception>(() => domain.ActualizarAsync(10, 1, intentoCambioUom));
            Assert.Contains("unidad de medida", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Entrega_ConUnidadAlternativa_DescuentaCantidadBaseYPrecioBase()
        {
            var repoHead = new Mock<IRepositorioGenerico<Entrega, int>>();
            repoHead.Setup(r => r.InsertarAsync(It.IsAny<Entrega>())).Callback<Entrega>(e => e.Entry = 50).ReturnsAsync((Entrega e) => e);
            var repoDet = new Mock<IRepositorioGenerico<EntregaDetalle, (int, int)>>();
            var repoNum = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            repoNum.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new NumeracionDocumentoDet { Serie = 1, Manual = "N", SigNumero = 100, Bloqueado = "N" });
            var tx = CrearTxMock();
            var asiento = new Mock<IInventarioAsientoService>();
            var movs = new List<MovimientoRequest>();
            asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((m, _) => movs.AddRange(m))
                .Returns(Task.CompletedTask);
            var docBase = new Mock<IDocumentoBaseService>();
            var credito = new Mock<ICreditoClienteService>();
            credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>()))
                .ReturnsAsync(new ResultadoCredito(true, null, null, 0m, 0m));
            var autCtx = new Mock<IAutorizacionCreditoContexto>();
            var repoAut = new Mock<IRepositorioGenerico<AutorizacionCredito, int>>();
            var unidades = CrearUnidadesMock();

            var domain = new EntregaDomain(
                repoHead.Object, repoDet.Object, repoNum.Object, tx.Object, asiento.Object,
                docBase.Object, credito.Object, autCtx.Object, repoAut.Object, unidades.Object);

            var linea = new EntregaDetalle { CodArticulo = "ART-1", CodAlmacen = "01", Cantidad = 2m, UomEntry = Caja, Precio = 120m };
            await domain.InsertarAsync(new Entrega { Serie = 1, CodigoSn = "CLI-1", TotalDoc = 240m }, new[] { linea });

            Assert.Equal(Caja, linea.UomEntry);
            Assert.Equal(12m, linea.FactorUom);
            Assert.Equal(24m, linea.CantidadBase);
            var mov = Assert.Single(movs);
            Assert.Equal(-24m, mov.Cantidad);
            Assert.Equal(10m, mov.PrecioUnitario);
        }

        [Fact]
        public async Task NotaDebito_PorArticulos_UsaCantidadBaseYPorConcepto_FijaFactorUno()
        {
            var repoHead = new Mock<IRepositorioGenerico<Factura, int>>();
            repoHead.Setup(r => r.InsertarAsync(It.IsAny<Factura>())).Callback<Factura>(f => f.Entry = 60).ReturnsAsync((Factura f) => f);
            var repoDet = new Mock<IRepositorioGenerico<FacturaDetalle, (int, int)>>();
            var repoNum = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            repoNum.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new NumeracionDocumentoDet { Serie = 1, CodigoObj = "6", SubTipoDoc = "ND", Manual = "N", SigNumero = 1, Bloqueado = "N" });
            var repoSocio = new Mock<IRepositorioGenerico<SocioNegocio, string>>();
            repoSocio.Setup(r => r.ObtenerAsync("CLI-1")).ReturnsAsync(new SocioNegocio { Codigo = "CLI-1", TipoSn = "C", Activo = "S" });
            var repoImp = new Mock<IRepositorioGenerico<Impuesto, string>>();
            repoImp.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA", Tasa = 12m });
            var repoMon = new Mock<IRepositorioGenerico<Monedum, string>>();
            repoMon.Setup(r => r.ObtenerAsync("QTZ")).ReturnsAsync(new Monedum { Codigo = "QTZ" });
            var repoArt = new Mock<IRepositorioGenerico<Articulo, string>>();
            repoArt.Setup(r => r.ObtenerAsync("ART-1")).ReturnsAsync(new Articulo { Codigo = "ART-1", Activo = "S" });
            var repoAlm = new Mock<IRepositorioGenerico<Almacen, string>>();
            repoAlm.Setup(r => r.ObtenerAsync("01")).ReturnsAsync(new Almacen { Codigo = "01" });
            var tx = CrearTxMock();
            var asiento = new Mock<IInventarioAsientoService>();
            var movs = new List<MovimientoRequest>();
            asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((m, _) => movs.AddRange(m))
                .Returns(Task.CompletedTask);
            var saldos = new Mock<ISaldoFacturaService>();
            var resolutor = new Mock<IResolutorVencimiento>();
            resolutor.Setup(r => r.ResolverAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(("CON", DateTime.Today));
            var unidades = CrearUnidadesMock();

            var domain = new NotaDebitoDomain(
                repoHead.Object, repoDet.Object, repoNum.Object, repoSocio.Object, repoImp.Object,
                repoMon.Object, repoArt.Object, repoAlm.Object, tx.Object, asiento.Object,
                saldos.Object, resolutor.Object, unidades.Object);

            var lineaArt = new FacturaDetalle { CodArticulo = "ART-1", CodAlmacen = "01", Cantidad = 2m, UomEntry = Caja, Precio = 60m, CodigoImpuesto = "IVA" };
            await domain.InsertarAsync(new Factura { Serie = 1, CodigoSn = "CLI-1", MonedaDoc = "QTZ", DocType = "I" }, new[] { lineaArt });

            Assert.Equal(Caja, lineaArt.UomEntry);
            Assert.Equal(12m, lineaArt.FactorUom);
            Assert.Equal(24m, lineaArt.CantidadBase);
            var mov = Assert.Single(movs);
            Assert.Equal(-24m, mov.Cantidad);
            Assert.Equal(5m, mov.PrecioUnitario);

            var lineaConcepto = new FacturaDetalle { Descripcion = "Recargo", Cantidad = 1m, Precio = 50m, CodigoImpuesto = "IVA" };
            await domain.InsertarAsync(new Factura { Serie = 1, CodigoSn = "CLI-1", MonedaDoc = "QTZ", DocType = "S" }, new[] { lineaConcepto });
            Assert.Null(lineaConcepto.UomEntry);
            Assert.Equal(1m, lineaConcepto.FactorUom);
            Assert.Equal(1m, lineaConcepto.CantidadBase);
        }

        [Fact]
        public async Task EntregaCompra_Y_FacturaCompra_ConUnidadAlternativa_IngresanCantidadBaseYPrecioBase()
        {
            var repoNum = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            repoNum.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new NumeracionDocumentoDet { Serie = 1, CodigoObj = "13", SubTipoDoc = "--", Manual = "N", SigNumero = 1, Bloqueado = "N" });
            var tx = CrearTxMock();
            var asiento = new Mock<IInventarioAsientoService>();
            var todosMovs = new List<MovimientoRequest>();
            asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((m, _) => todosMovs.AddRange(m))
                .Returns(Task.CompletedTask);
            var unidades = CrearUnidadesMock();

            // 1. EntregaCompra
            var repoEc = new Mock<IRepositorioGenerico<EntregaCompra, int>>();
            repoEc.Setup(r => r.InsertarAsync(It.IsAny<EntregaCompra>())).Callback<EntregaCompra>(e => e.Entry = 70).ReturnsAsync((EntregaCompra e) => e);
            var repoEcDet = new Mock<IRepositorioGenerico<EntregaCompraDetalle, (int, int)>>();
            var ecDomain = new EntregaCompraDomain(repoEc.Object, repoEcDet.Object, repoNum.Object, tx.Object, asiento.Object, unidades.Object);

            var lineaEc = new EntregaCompraDetalle { CodArticulo = "ART-1", CodAlmacen = "01", Cantidad = 3m, UomEntry = Caja, Precio = 240m };
            await ecDomain.InsertarAsync(new EntregaCompra { Serie = 1, CodigoSn = "PROV-1" }, new[] { lineaEc });

            Assert.Equal(36m, lineaEc.CantidadBase);
            Assert.Equal(36m, todosMovs[0].Cantidad);
            Assert.Equal(20m, todosMovs[0].PrecioUnitario);

            // 2. FacturaCompra
            var repoFc = new Mock<IRepositorioGenerico<FacturaCompra, int>>();
            repoFc.Setup(r => r.InsertarAsync(It.IsAny<FacturaCompra>())).Callback<FacturaCompra>(f => f.Entry = 71).ReturnsAsync((FacturaCompra f) => f);
            var repoFcDet = new Mock<IRepositorioGenerico<FacturaCompraDetalle, (int, int)>>();
            var saldos = new Mock<ISaldoFacturaCompraService>();
            var repoSocio = new Mock<IRepositorioGenerico<SocioNegocio, string>>();
            var resolutor = new Mock<IResolutorVencimiento>();
            resolutor.Setup(r => r.ResolverAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(("CON", DateTime.Today));

            var fcDomain = new FacturaCompraDomain(
                repoFc.Object, repoFcDet.Object, repoNum.Object, tx.Object, asiento.Object,
                saldos.Object, repoSocio.Object, resolutor.Object, unidades.Object);

            var lineaFc = new FacturaCompraDetalle { CodArticulo = "ART-1", CodAlmacen = "01", Cantidad = 2m, UomEntry = Caja, Precio = 180m };
            await fcDomain.InsertarAsync(new FacturaCompra { Serie = 1, CodigoSn = "PROV-1" }, new[] { lineaFc });

            Assert.Equal(24m, lineaFc.CantidadBase);
            Assert.Equal(24m, todosMovs[1].Cantidad);
            Assert.Equal(15m, todosMovs[1].PrecioUnitario);
        }

        [Fact]
        public async Task EntradaYSalidaMercancia_ConUnidadAlternativa_ValuanEnUnidadBase()
        {
            var repoNum = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            repoNum.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new NumeracionDocumentoDet { Serie = 1, Manual = "N", SigNumero = 1, Bloqueado = "N" });
            var tx = CrearTxMock();
            var asiento = new Mock<IInventarioAsientoService>();
            var todosMovs = new List<MovimientoRequest>();
            asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((m, _) => todosMovs.AddRange(m))
                .Returns(Task.CompletedTask);
            var repoArt = new Mock<IRepositorioGenerico<Articulo, string>>();
            repoArt.Setup(r => r.ObtenerAsync("ART-1")).ReturnsAsync(new Articulo { Codigo = "ART-1", MetodoValuacion = "P", CostoPromedio = 10m });
            var unidades = CrearUnidadesMock();

            // 1. EntradaMercancia sin costo explícito -> toma CostoPromedio (10) * Factor (12) = 120 por Caja; al kardex va 24 a 10
            var repoEm = new Mock<IRepositorioGenerico<EntradaMercancia, int>>();
            repoEm.Setup(r => r.InsertarAsync(It.IsAny<EntradaMercancia>())).Callback<EntradaMercancia>(e => e.Entry = 80).ReturnsAsync((EntradaMercancia e) => e);
            var repoEmDet = new Mock<IRepositorioGenerico<EntradaMercanciaDetalle, (int, int)>>();
            var emDomain = new EntradaMercanciaDomain(repoEm.Object, repoEmDet.Object, repoNum.Object, tx.Object, asiento.Object, repoArt.Object, unidades.Object);

            var lineaEm = new EntradaMercanciaDetalle { CodArticulo = "ART-1", CodAlmacen = "01", Cantidad = 2m, UomEntry = Caja };
            var docEm = new EntradaMercancia { Serie = 1 };
            await emDomain.InsertarAsync(docEm, new[] { lineaEm });

            Assert.Equal(120m, lineaEm.CostoUnitario);
            Assert.Equal(240m, lineaEm.TotalLinea);
            Assert.Equal(240m, docEm.TotalDoc);
            Assert.Equal(24m, todosMovs[0].Cantidad);
            Assert.Equal(10m, todosMovs[0].PrecioUnitario);

            // 2. SalidaMercancia -> CostoUnitario de línea = 10 * 12 = 120; al kardex va -24 a 10
            var repoSm = new Mock<IRepositorioGenerico<SalidaMercancia, int>>();
            repoSm.Setup(r => r.InsertarAsync(It.IsAny<SalidaMercancia>())).Callback<SalidaMercancia>(s => s.Entry = 81).ReturnsAsync((SalidaMercancia s) => s);
            var repoSmDet = new Mock<IRepositorioGenerico<SalidaMercanciaDetalle, (int, int)>>();
            var smDomain = new SalidaMercanciaDomain(repoSm.Object, repoSmDet.Object, repoNum.Object, tx.Object, asiento.Object, repoArt.Object, unidades.Object);

            var lineaSm = new SalidaMercanciaDetalle { CodArticulo = "ART-1", CodAlmacen = "01", Cantidad = 2m, UomEntry = Caja };
            var docSm = new SalidaMercancia { Serie = 1 };
            await smDomain.InsertarAsync(docSm, new[] { lineaSm });

            Assert.Equal(120m, lineaSm.CostoUnitario);
            Assert.Equal(240m, lineaSm.TotalLinea);
            Assert.Equal(-24m, todosMovs[1].Cantidad);
            Assert.Equal(10m, todosMovs[1].PrecioUnitario);
        }

        [Fact]
        public async Task OrdenServicioLineaService_PrepararYAsentarSalida_UsaCantidadBaseYPrecioBase()
        {
            var repoArt = new Mock<IRepositorioGenerico<Articulo, string>>();
            repoArt.Setup(r => r.ObtenerAsync("ART-1")).ReturnsAsync(new Articulo
            {
                Codigo = "ART-1", Nombre = "Aceite", Activo = "S", ArticuloVenta = "S", ArticuloInventario = "S", AlmacenDefecto = "01"
            });
            var repoImp = new Mock<IRepositorioGenerico<Impuesto, string>>();
            repoImp.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA", Tasa = 12m });
            var repoMec = new Mock<IRepositorioGenerico<Mecanico, string>>();
            var asiento = new Mock<IInventarioAsientoService>();
            var movs = new List<MovimientoRequest>();
            asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((m, _) => movs.AddRange(m))
                .Returns(Task.CompletedTask);
            var unidades = CrearUnidadesMock();

            var srv = new OrdenServicioLineaService(repoArt.Object, repoImp.Object, repoMec.Object, asiento.Object, unidades.Object);

            var linea = new OrdenServicioDetalle { NoLinea = 1, CodArticulo = "ART-1", Cantidad = 2m, UomEntry = Caja, Precio = 120m, CodigoImpuesto = "IVA" };
            await srv.PrepararAsync(new List<OrdenServicioDetalle> { linea });
            await srv.AsentarSalidaAsync(99, DateTime.Today, new[] { linea });

            Assert.Equal(Caja, linea.UomEntry);
            Assert.Equal(12m, linea.FactorUom);
            Assert.Equal(24m, linea.CantidadBase);
            var mov = Assert.Single(movs);
            Assert.Equal(-24m, mov.Cantidad);
            Assert.Equal(10m, mov.PrecioUnitario);
        }

        [Fact]
        public async Task ArticuloDomain_CambiarGrupoUnidadMedidaConMovimientosOLineas_SeRechaza()
        {
            var artConMov = new Articulo
            {
                Codigo = "ART-1",
                CodigoGrpUnidadMedida = 10,
                MovimientoInventarios = new List<MovimientoInventario> { new() { Entry = 1, CodArticulo = "ART-1" } }
            };
            var repoArt = new Mock<IRepositorioGenerico<Articulo, string>>();
            repoArt.Setup(r => r.ObtenerAsync("ART-1")).ReturnsAsync(artConMov);
            repoArt.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<Articulo> { artConMov }.AsAsyncQueryable());
            repoArt.Setup(r => r.ActualizarAsync("ART-1", It.IsAny<Articulo>())).ReturnsAsync(true);

            var repoGrupo = new Mock<IRepositorioGenerico<GrupoUnidadMedidaArticulo, int>>();
            var repoDet = new Mock<IRepositorioGenerico<GrupoUnidadMedidaDetArticulo, (int, int)>>();
            var repoUnidad = new Mock<IRepositorioGenerico<UnidadMedidaArticulo, int>>();
            var repoNum = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();

            var uomSrv = new UnidadMedidaService(repoArt.Object, repoGrupo.Object, repoDet.Object, repoUnidad.Object);
            var artDomain = new ArticuloDomain(repoArt.Object, repoNum.Object, uomSrv);

            // Cambiar de grupo 10 a 20 con movimientos -> lanza excepción
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                artDomain.ActualizarAsync("ART-1", new Articulo { Codigo = "ART-1", CodigoGrpUnidadMedida = 20 }));
            Assert.Contains("grupo de unidad de medida", ex.Message, StringComparison.OrdinalIgnoreCase);

            // Conservar el mismo grupo 10 -> permite actualizar
            Assert.True(await artDomain.ActualizarAsync("ART-1", new Articulo { Codigo = "ART-1", CodigoGrpUnidadMedida = 10, Nombre = "Renombrado" }));
        }
    }
}
