using System.Data;
using System.Globalization;
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class NotaCreditoCompraDomainTests
    {
        // ---- Constantes propias del documento (en la nota de credito de proveedor cambian estas lineas) ----
        private const string CodigoObj = "15";
        private const string CodigoObjOtro = "13";   // CodigoObj de otro documento (FacturaCompra)
        private const string TipoSocio = "P";
        private const string TipoSocioOpuesto = "C";
        private const int TipoBase = 13;
        private const string MsgSerieOtroObjeto = "La serie no corresponde a notas de crédito de proveedor.";
        private const string MsgSocioTipo = "El socio de negocio debe ser un proveedor.";
        private const string MsgBaseRequerida = "Indique la factura de compra base.";
        private const string MsgBaseNoExiste = "La factura de compra base no existe.";
        private const string MsgBaseCancelada = "La factura de compra base está cancelada.";
        private const string MsgBaseOtroSocio = "La factura de compra base pertenece a otro socio de negocio.";
        private const string MsgBaseMoneda = "La moneda de la nota de crédito debe ser la misma de la factura de compra base.";
        private const string MsgExceso = "El total acreditado ({0}) excede el total de la factura de compra base ({1}).";
        private const string MsgFacturaNoExiste = "La factura de compra no existe.";
        private const string MsgFacturaCancelada = "La factura de compra está cancelada.";
        // ---- fin de constantes ----

        private readonly Mock<IRepositorioGenerico<NotaCreditoCompra, int>> _repoNota = new();
        private readonly Mock<IRepositorioGenerico<NotaCreditoCompraDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<Impuesto, string>> _repoImpuesto = new();
        private readonly Mock<IRepositorioGenerico<Monedum, string>> _repoMoneda = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoFacturaCompra = new();
        private readonly Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>> _repoFacturaCompraDetalle = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly NotaCreditoCompraDomain _domain;

        public NotaCreditoCompraDomainTests()
        {
            _domain = new NotaCreditoCompraDomain(
                _repoNota.Object, _repoDetalle.Object, _repoNumeracion.Object, _repoSocio.Object,
                _repoImpuesto.Object, _repoMoneda.Object, _repoFacturaCompra.Object, _repoFacturaCompraDetalle.Object, _tx.Object);

            // El doble del ejecutor corre el Func directo (sin transaccion ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IsolationLevel>()))
                .Returns<Func<Task<int>>, IsolationLevel>((f, _) => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoNota.Setup(r => r.InsertarAsync(It.IsAny<NotaCreditoCompra>()))
                .ReturnsAsync((NotaCreditoCompra n) => { n.Entry = 99; return n; });
            _repoNota.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<NotaCreditoCompra>().AsAsyncQueryable());
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<NotaCreditoCompraDetalle>())).Returns(Task.CompletedTask);

            // Valores por defecto validos: cada test rompe solo lo que quiere probar.
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie());
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", TipoSn = TipoSocio, Activo = "S" });
            _repoMoneda.Setup(r => r.ObtenerAsync("QTZ")).ReturnsAsync(new Monedum { Codigo = "QTZ" });
            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA", Tasa = 12m });
        }

        private static NumeracionDocumentoDet CrearSerie(int? sig = 5, int? fin = null, string bloqueado = "N", string manual = "N", string? codigoObj = null) => new()
        {
            CodigoObj = codigoObj ?? CodigoObj, Serie = 4, NombreSerie = "Primario",
            SigNumero = sig, FinNumero = fin, Bloqueado = bloqueado, Manual = manual,
            SubTipoDoc = "--", TipoSerie = "D"
        };

        private static NotaCreditoCompra Nota(Action<NotaCreditoCompra>? ajustar = null)
        {
            var n = new NotaCreditoCompra
            {
                Serie = 4, CodigoSn = "SN1", MonedaDoc = "QTZ",
                FechaDoc = new DateTime(2026, 10, 4), FechaEmision = new DateTime(2026, 10, 4)
            };
            ajustar?.Invoke(n);
            return n;
        }

        // Linea valida por defecto: 1 x 100, sin descuento, IVA 12% => total de linea 112.
        private static NotaCreditoCompraDetalle Linea(Action<NotaCreditoCompraDetalle>? ajustar = null)
        {
            var l = new NotaCreditoCompraDetalle
            {
                Descripcion = "Ajuste de precio", Cantidad = 1m, Precio = 100m, PrctjeDesc = 0m, CodigoImpuesto = "IVA"
            };
            ajustar?.Invoke(l);
            return l;
        }

        private async Task<string> MensajeAlInsertar(NotaCreditoCompra nota, params NotaCreditoCompraDetalle[] lineas)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(nota, lineas));
            return ex.Message;
        }

        private static string Exceso(string acreditado, string total) =>
            string.Format(CultureInfo.InvariantCulture, MsgExceso, acreditado, total);

        // ---------------- InsertarAsync: camino feliz ----------------

        [Fact]
        public async Task InsertarAsync_Valida_NumeraGuardaEncabezadoYLineas()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var nota = Nota();

            var entry = await _domain.InsertarAsync(nota, new[] { Linea(), Linea(l => l.Descripcion = "Descuento comercial") });

            Assert.Equal(99, entry);
            Assert.Equal(CodigoObj, nota.TipoObjeto);
            Assert.Equal(5, nota.NumDoc);
            Assert.Equal(6, serie.SigNumero);          // arrancaba en 5: avanzo
            Assert.Equal("N", nota.NumManual);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), IsolationLevel.Serializable), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<NotaCreditoCompraDetalle>(l => l.Entry == 99 && l.NoLinea == 1 && l.TipoObjeto == CodigoObj)), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<NotaCreditoCompraDetalle>(l => l.Entry == 99 && l.NoLinea == 2 && l.Descripcion == "Descuento comercial")), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_FuerzaLosCamposDeSistema()
        {
            var nota = Nota(n =>
            {
                n.Cancelado = "S"; n.EstadoDoc = "C"; n.Imprimido = "S"; n.FechaCancelado = DateTime.Now; n.TipoObjeto = "6";
            });

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal("N", nota.Cancelado);
            Assert.Equal("A", nota.EstadoDoc);
            Assert.Equal("N", nota.Imprimido);
            Assert.Null(nota.FechaCancelado);
            Assert.Equal(CodigoObj, nota.TipoObjeto);
        }

        [Fact]
        public async Task InsertarAsync_SinFechas_UsaLaFechaActual()
        {
            var nota = Nota(n => { n.FechaDoc = null; n.FechaEmision = null; });

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.NotNull(nota.FechaDoc);
            Assert.Equal(nota.FechaDoc, nota.FechaEmision);
        }

        [Fact]
        public async Task InsertarAsync_SerieManualConNumDoc_UsaElNumeroYMarcaNumManual()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: null, manual: "S"));
            var nota = Nota(n => n.NumDoc = 321);

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal(321, nota.NumDoc);
            Assert.Equal("S", nota.NumManual);
        }

        // ---------------- InsertarAsync: totales calculados por el servidor ----------------

        [Fact]
        public async Task InsertarAsync_RecalculaLineasYTotales_IgnorandoLoQueMandaElCliente()
        {
            // L1: 3 x 10.10 = 30.30; desc 10% = 3.03; subtotal 27.27; IVA 12% = 3.2724 -> 3.27; total 30.54
            // L2: 2 x 5 = 10.00; sin descuento; IVA 12% = 1.20; total 11.20
            var l1 = Linea(l => { l.Cantidad = 3m; l.Precio = 10.10m; l.PrctjeDesc = 10m; l.Impuesto = 999m; l.TotalLinea = 999m; l.PrecioBruto = 999m; });
            var l2 = Linea(l => { l.Cantidad = 2m; l.Precio = 5m; l.Impuesto = 888m; l.TotalLinea = 888m; });
            var nota = Nota(n => { n.TotalBruto = 1m; n.TotalDesc = 1m; n.TotalImp = 1m; n.TotalDoc = 1m; });

            await _domain.InsertarAsync(nota, new[] { l1, l2 });

            Assert.Equal(30.30m, l1.PrecioBruto);
            Assert.Equal(3.27m, l1.Impuesto);
            Assert.Equal(30.54m, l1.TotalLinea);
            Assert.Equal(10.00m, l2.PrecioBruto);
            Assert.Equal(1.20m, l2.Impuesto);
            Assert.Equal(11.20m, l2.TotalLinea);
            Assert.Equal(40.30m, nota.TotalBruto);
            Assert.Equal(3.03m, nota.TotalDesc);
            Assert.Equal(4.47m, nota.TotalImp);
            Assert.Equal(41.74m, nota.TotalDoc);
        }

        [Fact]
        public async Task InsertarAsync_Redondea_ALaAlzaEnElPuntoMedio()
        {
            var linea = Linea(l => { l.Cantidad = 1m; l.Precio = 0.005m; });
            var nota = Nota();

            await _domain.InsertarAsync(nota, new[] { linea });

            Assert.Equal(0.01m, linea.PrecioBruto);   // 0.005 -> 0.01 (AwayFromZero)
            Assert.Equal(0.00m, linea.Impuesto);      // 0.01 x 12% = 0.0012 -> 0.00
            Assert.Equal(0.01m, linea.TotalLinea);
        }

        [Fact]
        public async Task InsertarAsync_ImpuestoExentoOSinTasa_NoSumaImpuesto()
        {
            _repoImpuesto.Setup(r => r.ObtenerAsync("EXE")).ReturnsAsync(new Impuesto { Codigo = "EXE", Tasa = 0m });
            _repoImpuesto.Setup(r => r.ObtenerAsync("SINTASA")).ReturnsAsync(new Impuesto { Codigo = "SINTASA", Tasa = null });
            var l1 = Linea(l => l.CodigoImpuesto = "EXE");
            var l2 = Linea(l => l.CodigoImpuesto = "SINTASA");
            var nota = Nota();

            await _domain.InsertarAsync(nota, new[] { l1, l2 });

            Assert.Equal(0m, l1.Impuesto);
            Assert.Equal(100m, l1.TotalLinea);
            Assert.Equal(0m, l2.Impuesto);
            Assert.Equal(200m, nota.TotalDoc);
        }

        // ---------------- InsertarAsync: serie ----------------

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            Assert.Equal("La serie no existe.", await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SerieDeOtroObjeto_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(codigoObj: CodigoObjOtro));

            Assert.Equal(MsgSerieOtroObjeto, await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(bloqueado: "S"));

            Assert.Equal("La serie está bloqueada y no se puede usar para registrar notas de crédito.", await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumDoc_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: null, manual: "S"));

            Assert.Equal("El número de documento es requerido para series manuales.", await MensajeAlInsertar(Nota(n => n.NumDoc = 0), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SerieSinSiguienteNumero_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: null));

            Assert.Equal("La serie no tiene configurado el número siguiente.", await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: 11, fin: 10));

            Assert.Equal("Se agotó la numeración disponible en esta serie.", await MensajeAlInsertar(Nota(), Linea()));
        }

        // ---------------- InsertarAsync: socio y moneda ----------------

        [Fact]
        public async Task InsertarAsync_SinSocio_Lanza()
        {
            Assert.Equal("El socio de negocio es requerido.", await MensajeAlInsertar(Nota(n => n.CodigoSn = ""), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SocioInexistente_Lanza()
        {
            Assert.Equal("El socio de negocio no existe.", await MensajeAlInsertar(Nota(n => n.CodigoSn = "NOEXISTE"), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SocioDeTipoEquivocado_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", TipoSn = TipoSocioOpuesto, Activo = "S" });

            Assert.Equal(MsgSocioTipo, await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SocioInactivo_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", TipoSn = TipoSocio, Activo = "N" });

            Assert.Equal("El socio de negocio está inactivo.", await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SinMoneda_Lanza()
        {
            Assert.Equal("La moneda es requerida.", await MensajeAlInsertar(Nota(n => n.MonedaDoc = null), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_MonedaInexistente_Lanza()
        {
            Assert.Equal("La moneda no existe.", await MensajeAlInsertar(Nota(n => n.MonedaDoc = "XXX"), Linea()));
        }

        // ---------------- InsertarAsync: lineas ----------------

        [Fact]
        public async Task InsertarAsync_SinLineas_Lanza()
        {
            Assert.Equal("El documento debe tener al menos una línea.", await MensajeAlInsertar(Nota()));
        }

        [Theory]
        [InlineData("", "Línea 1: la descripción es requerida.")]
        [InlineData("   ", "Línea 1: la descripción es requerida.")]
        public async Task InsertarAsync_LineaSinDescripcion_Lanza(string descripcion, string esperado)
        {
            Assert.Equal(esperado, await MensajeAlInsertar(Nota(), Linea(l => l.Descripcion = descripcion)));
        }

        [Fact]
        public async Task InsertarAsync_LineaConDescripcionMuyLarga_Lanza()
        {
            Assert.Equal("Línea 1: la descripción no puede superar 200 caracteres.",
                await MensajeAlInsertar(Nota(), Linea(l => l.Descripcion = new string('x', 201))));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public async Task InsertarAsync_LineaConCantidadNoPositiva_Lanza(int cantidad)
        {
            Assert.Equal("Línea 1: la cantidad debe ser mayor que 0.", await MensajeAlInsertar(Nota(), Linea(l => l.Cantidad = cantidad)));
        }

        [Fact]
        public async Task InsertarAsync_LineaSinPrecio_Lanza()
        {
            Assert.Equal("Línea 1: el precio es requerido.", await MensajeAlInsertar(Nota(), Linea(l => l.Precio = null)));
        }

        [Fact]
        public async Task InsertarAsync_LineaConPrecioNegativo_Lanza()
        {
            Assert.Equal("Línea 1: el precio no puede ser negativo.", await MensajeAlInsertar(Nota(), Linea(l => l.Precio = -1m)));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        public async Task InsertarAsync_LineaConDescuentoFueraDeRango_Lanza(int descuento)
        {
            Assert.Equal("Línea 1: el descuento debe estar entre 0 y 100.", await MensajeAlInsertar(Nota(), Linea(l => l.PrctjeDesc = descuento)));
        }

        [Fact]
        public async Task InsertarAsync_LineaSinImpuesto_Lanza()
        {
            Assert.Equal("Línea 1: el impuesto es requerido.", await MensajeAlInsertar(Nota(), Linea(l => l.CodigoImpuesto = " ")));
        }

        [Fact]
        public async Task InsertarAsync_LineaConImpuestoInexistente_Lanza()
        {
            Assert.Equal("Línea 2: el impuesto 'ZZZ' no existe.",
                await MensajeAlInsertar(Nota(), Linea(), Linea(l => l.CodigoImpuesto = "ZZZ")));
        }

        [Fact]
        public async Task InsertarAsync_RecortaLaDescripcionYNormalizaDescuentoNulo()
        {
            var linea = Linea(l => { l.Descripcion = "  Flete  "; l.PrctjeDesc = null; });

            await _domain.InsertarAsync(Nota(), new[] { linea });

            Assert.Equal("Flete", linea.Descripcion);
            Assert.Equal(0m, linea.PrctjeDesc);
        }

        [Fact]
        public async Task InsertarAsync_ConErrorDeValidacion_NoGuardaNada()
        {
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nota(), Array.Empty<NotaCreditoCompraDetalle>()));

            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IsolationLevel>()), Times.Never);
            _repoNota.Verify(r => r.InsertarAsync(It.IsAny<NotaCreditoCompra>()), Times.Never);
        }

        // ---------------- InsertarAsync: factura base ----------------

        private void PrepararFactura(int entry = 12, string socio = "SN1", string cancelado = "N", string moneda = "QTZ", decimal total = 1000m, string subTipo = "--") =>
            _repoFacturaCompra.Setup(r => r.ObtenerAsync(entry)).ReturnsAsync(new FacturaCompra
            {
                Entry = entry, CodigoSn = socio, Cancelado = cancelado, MonedaDoc = moneda, TotalDoc = total, DocSubType = subTipo
            });

        private static NotaCreditoCompra NotaConBase(int entry = 12) => Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = entry; });

        private static NotaCreditoCompra Previa(int baseEntry, decimal total, string cancelado = "N", int baseTipo = TipoBase) =>
            new() { BaseTipo = baseTipo, BaseEntry = baseEntry, TotalDoc = total, Cancelado = cancelado };

        private void NotasPrevias(params NotaCreditoCompra[] notas) =>
            _repoNota.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(notas.ToList().AsAsyncQueryable());

        [Fact]
        public async Task InsertarAsync_ConBaseValida_ConservaLaReferencia()
        {
            PrepararFactura();
            var nota = NotaConBase();

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal(TipoBase, nota.BaseTipo);
            Assert.Equal(12, nota.BaseEntry);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(-1)]
        public async Task InsertarAsync_SinBase_NormalizaBaseTipoMenosUnoYBaseEntryNulo(int? baseTipo)
        {
            var nota = Nota(n => { n.BaseTipo = baseTipo; n.BaseEntry = 55; });

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal(-1, nota.BaseTipo);
            Assert.Null(nota.BaseEntry);
        }

        [Fact]
        public async Task InsertarAsync_ConTipoBaseInvalido_Lanza()
        {
            Assert.Equal("El tipo de documento base no es válido.", await MensajeAlInsertar(Nota(n => n.BaseTipo = 99), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseSinEntry_Lanza()
        {
            Assert.Equal(MsgBaseRequerida, await MensajeAlInsertar(Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = null; }), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseInexistente_Lanza()
        {
            Assert.Equal(MsgBaseNoExiste, await MensajeAlInsertar(NotaConBase(777), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseCancelada_Lanza()
        {
            PrepararFactura(cancelado: "S");

            Assert.Equal(MsgBaseCancelada, await MensajeAlInsertar(NotaConBase(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseDeOtroSocio_Lanza()
        {
            PrepararFactura(socio: "OTRO");

            Assert.Equal(MsgBaseOtroSocio, await MensajeAlInsertar(NotaConBase(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseDeOtraMoneda_Lanza()
        {
            PrepararFactura(moneda: "USD");

            Assert.Equal(MsgBaseMoneda, await MensajeAlInsertar(NotaConBase(), Linea()));
            _repoNota.Verify(r => r.InsertarAsync(It.IsAny<NotaCreditoCompra>()), Times.Never);
        }

        // ---------------- InsertarAsync: limite acreditable ----------------

        [Fact]
        public async Task InsertarAsync_TotalIgualAlDeLaFactura_EsValido()
        {
            PrepararFactura(total: 112m);          // la nota (1 x 100 + IVA 12%) suma exactamente 112

            var entry = await _domain.InsertarAsync(NotaConBase(), new[] { Linea() });

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task InsertarAsync_ConBaseUnaNotaDeDebito_LaAceptaYAplicaElLimiteSobreSuTotal()
        {
            PrepararFactura(total: 112m, subTipo: "DN");

            var entry = await _domain.InsertarAsync(NotaConBase(), new[] { Linea() });   // la nota suma exactamente 112

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task InsertarAsync_ConBaseUnaNotaDeDebito_QueSeExcede_Lanza()
        {
            PrepararFactura(total: 100m, subTipo: "DN");

            Assert.Equal(Exceso("112.00", "100.00"), await MensajeAlInsertar(NotaConBase(), Linea()));
        }

        [Fact]
        public async Task ObtenerFacturasCompraBaseAsync_IncluyeLasNotasDeDebitoDelSocio()
        {
            var filas = new List<FacturaCompra>
            {
                new() { Entry = 1, CodigoSn = "SN1", Cancelado = "N", TotalDoc = 112m, DocSubType = "--" },
                new() { Entry = 2, CodigoSn = "SN1", Cancelado = "N", TotalDoc = 50m, DocSubType = "DN" }
            };
            _repoFacturaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());
            NotasPrevias(Previa(2, 20m));

            var resultado = (await _domain.ObtenerFacturasCompraBaseAsync("SN1")).ToList();

            Assert.Equal(new[] { 2, 1 }, resultado.Select(f => f.Documento.Entry));
            Assert.Equal("DN", resultado[0].Documento.DocSubType);
            Assert.Equal(20m, resultado[0].TotalAcreditado);
        }

        [Fact]
        public async Task InsertarAsync_TotalMayorAlDeLaFactura_LanzaYNoGuarda()
        {
            PrepararFactura(total: 100m);

            Assert.Equal(Exceso("112.00", "100.00"), await MensajeAlInsertar(NotaConBase(), Linea()));
            _repoNota.Verify(r => r.InsertarAsync(It.IsAny<NotaCreditoCompra>()), Times.Never);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<NotaCreditoCompraDetalle>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_ConNotasPreviasQueSumanMasDelTotal_Lanza()
        {
            PrepararFactura(total: 200m);
            NotasPrevias(Previa(12, 50m), Previa(12, 40m));   // 90 ya acreditado; la nueva es 112 => 202 > 200

            Assert.Equal(Exceso("202.00", "200.00"), await MensajeAlInsertar(NotaConBase(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConNotasPreviasDentroDelLimite_EsValido()
        {
            PrepararFactura(total: 200m);
            NotasPrevias(Previa(12, 50m));                    // 50 + 112 = 162 <= 200

            var entry = await _domain.InsertarAsync(NotaConBase(), new[] { Linea() });

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task InsertarAsync_IgnoraLasNotasPreviasCanceladas_DeOtraFacturaOSinBase()
        {
            PrepararFactura(total: 120m);
            NotasPrevias(
                Previa(12, 100m, cancelado: "S"),             // cancelada: libera su monto
                Previa(99, 500m),                             // de otra factura
                Previa(12, 300m, baseTipo: -1));              // sin base de factura de compra

            var entry = await _domain.InsertarAsync(NotaConBase(), new[] { Linea() });   // 112 <= 120

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task InsertarAsync_ComparaALosDosDecimales()
        {
            PrepararFactura(total: 111.996m);                 // se redondea a 112.00; sin redondear, 112.00 > 111.996 lanzaria

            var entry = await _domain.InsertarAsync(NotaConBase(), new[] { Linea() });

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task InsertarAsync_ExcedeElTotalPorUnCentavo_LanzaYNoGuarda()
        {
            PrepararFactura(total: 111.99m);                  // la nota suma 112.00: excede por 0.01

            Assert.Equal(Exceso("112.00", "111.99"), await MensajeAlInsertar(NotaConBase(), Linea()));
            _repoNota.Verify(r => r.InsertarAsync(It.IsAny<NotaCreditoCompra>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_ElLimiteSeConsultaDentroDeLaTransaccion()
        {
            PrepararFactura(total: 100m);                     // excederia, pero la transaccion no corre el Func
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IsolationLevel>()))
                .Returns(Task.FromResult(0));

            var entry = await _domain.InsertarAsync(NotaConBase(), new[] { Linea() });

            Assert.Equal(0, entry);
            _repoNota.Verify(r => r.ObtenerTodoAsync(), Times.Never);     // el limite vive dentro del Func
            _repoNota.Verify(r => r.InsertarAsync(It.IsAny<NotaCreditoCompra>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SinBase_NoTieneLimiteNiConsultaLasNotasPrevias()
        {
            var linea = Linea(l => l.Precio = 1_000_000m);

            var entry = await _domain.InsertarAsync(Nota(), new[] { linea });

            Assert.Equal(99, entry);
            _repoNota.Verify(r => r.ObtenerTodoAsync(), Times.Never);
            _repoFacturaCompra.Verify(r => r.ObtenerAsync(It.IsAny<int>()), Times.Never);
        }

        // ---------------- ActualizarAsync ----------------

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalse()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync((NotaCreditoCompra?)null);

            Assert.False(await _domain.ActualizarAsync(5, new NotaCreditoCompra()));
        }

        [Fact]
        public async Task ActualizarAsync_Cancelar_MarcaCanceladoYFechaSinTocarElComentario()
        {
            var existente = new NotaCreditoCompra { Entry = 5, Cancelado = "N", EstadoDoc = "A", Comentario = "original" };
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(existente);

            var ok = await _domain.ActualizarAsync(5, new NotaCreditoCompra { Cancelado = "S" });

            Assert.True(ok);
            Assert.Equal("S", existente.Cancelado);
            Assert.NotNull(existente.FechaCancelado);
            Assert.Equal("original", existente.Comentario);
            Assert.Equal("A", existente.EstadoDoc);   // EstadoDoc no cambia al cancelar
        }

        [Fact]
        public async Task ActualizarAsync_CancelarConComentario_ActualizaElComentario()
        {
            var existente = new NotaCreditoCompra { Entry = 5, Cancelado = "N", Comentario = "original" };
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(existente);

            await _domain.ActualizarAsync(5, new NotaCreditoCompra { Cancelado = "S", Comentario = "motivo" });

            Assert.Equal("motivo", existente.Comentario);
        }

        [Fact]
        public async Task ActualizarAsync_SoloComentario_NoCancelaNiCambiaOtrosCampos()
        {
            var existente = new NotaCreditoCompra { Entry = 5, Cancelado = "N", Comentario = "viejo", TotalDoc = 50m, CodigoSn = "SN1" };
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(existente);

            await _domain.ActualizarAsync(5, new NotaCreditoCompra { Cancelado = "N", Comentario = "nuevo", TotalDoc = 999m, CodigoSn = "OTRO" });

            Assert.Equal("nuevo", existente.Comentario);
            Assert.Equal("N", existente.Cancelado);
            Assert.Equal(50m, existente.TotalDoc);
            Assert.Equal("SN1", existente.CodigoSn);
        }

        [Fact]
        public async Task ActualizarAsync_DocumentoCancelado_NoSePuedeModificar()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new NotaCreditoCompra { Entry = 5, Cancelado = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, new NotaCreditoCompra { Comentario = "x" }));

            Assert.Equal("El documento está cancelado y no se puede modificar.", ex.Message);
        }

        // ---------------- EliminarAsync ----------------

        [Fact]
        public async Task EliminarAsync_Inexistente_DevuelveFalse()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync((NotaCreditoCompra?)null);

            Assert.False(await _domain.EliminarAsync(5));
        }

        [Fact]
        public async Task EliminarAsync_SinCancelar_Lanza()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new NotaCreditoCompra { Entry = 5, Cancelado = "N" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(5));

            Assert.Equal("Cancele el documento (Cancelado='S') antes de eliminarlo.", ex.Message);
            _repoNota.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_Cancelado_BorraLasLineasYElEncabezado()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new NotaCreditoCompra { Entry = 5, Cancelado = "S" });
            var lineas = new List<NotaCreditoCompraDetalle>
            {
                new() { Entry = 5, NoLinea = 1 }, new() { Entry = 5, NoLinea = 2 }, new() { Entry = 6, NoLinea = 1 }
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).ReturnsAsync(true);
            _repoNota.Setup(r => r.EliminarAsync(5)).ReturnsAsync(true);

            var ok = await _domain.EliminarAsync(5);

            Assert.True(ok);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 5 && k.NoLinea == 1)), Times.Once);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 5 && k.NoLinea == 2)), Times.Once);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 6 && k.NoLinea == 1)), Times.Never);
            _repoNota.Verify(r => r.EliminarAsync(5), Times.Once);
        }

        // ---------------- Consultas ----------------

        [Fact]
        public async Task ObtenerLineasAsync_DevuelveSoloLasDelDocumentoEnOrden()
        {
            var lineas = new List<NotaCreditoCompraDetalle>
            {
                new() { Entry = 5, NoLinea = 2 }, new() { Entry = 6, NoLinea = 1 }, new() { Entry = 5, NoLinea = 1 }
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());

            var resultado = (await _domain.ObtenerLineasAsync(5)).ToList();

            Assert.Equal(new[] { 1, 2 }, resultado.Select(l => l.NoLinea));
        }

        [Fact]
        public async Task ObtenerFacturasCompraBaseAsync_DevuelveLasNoCanceladasDelSocioConLoYaAcreditado()
        {
            var facturas = new List<FacturaCompra>
            {
                new() { Entry = 1, CodigoSn = "SN1", Cancelado = "N", TotalDoc = 112m },
                new() { Entry = 2, CodigoSn = "SN1", Cancelado = "S", TotalDoc = 50m },
                new() { Entry = 3, CodigoSn = "OTRO", Cancelado = "N", TotalDoc = 70m },
                new() { Entry = 4, CodigoSn = "SN1", Cancelado = "N", TotalDoc = 200m }
            };
            _repoFacturaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            NotasPrevias(Previa(1, 30m), Previa(1, 20m, cancelado: "S"), Previa(3, 10m));

            var resultado = (await _domain.ObtenerFacturasCompraBaseAsync("SN1")).ToList();

            Assert.Equal(new[] { 4, 1 }, resultado.Select(f => f.Documento.Entry));   // de la mas nueva a la mas vieja
            Assert.Equal(0m, resultado[0].TotalAcreditado);
            Assert.Equal(30m, resultado[1].TotalAcreditado);                           // la cancelada no cuenta
        }

        [Fact]
        public async Task ObtenerFacturaCompraBaseAsync_DevuelveEncabezadoLineasOrdenadasYAcreditado()
        {
            PrepararFactura(entry: 12, total: 112m);
            var lineas = new List<FacturaCompraDetalle>
            {
                new() { Entry = 12, NoLinea = 2 }, new() { Entry = 13, NoLinea = 1 }, new() { Entry = 12, NoLinea = 1 }
            };
            _repoFacturaCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());
            NotasPrevias(Previa(12, 25m), Previa(12, 10m, cancelado: "S"));

            var resultado = await _domain.ObtenerFacturaCompraBaseAsync(12);

            Assert.Equal(12, resultado.Documento.Entry);
            Assert.Equal(new[] { 1, 2 }, resultado.Lineas.Select(l => l.NoLinea));
            Assert.Equal(25m, resultado.TotalAcreditado);
        }

        [Fact]
        public async Task ObtenerFacturaCompraBaseAsync_Inexistente_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ObtenerFacturaCompraBaseAsync(777));

            Assert.Equal(MsgFacturaNoExiste, ex.Message);
        }

        [Fact]
        public async Task ObtenerFacturaCompraBaseAsync_Cancelada_Lanza()
        {
            PrepararFactura(entry: 12, cancelado: "S");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ObtenerFacturaCompraBaseAsync(12));

            Assert.Equal(MsgFacturaCancelada, ex.Message);
        }
    }
}
