using API.Domain.Core;
using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class NotaDebitoDomainTests
    {
        // ---- Constantes propias del documento (en la nota de debito de proveedor cambian estas lineas) ----
        private const string CodigoObj = "6";
        private const string CodigoObjOtro = "5";   // CodigoObj de otro documento (Entrega)
        private const string SubTipoNota = "ND";
        private const string TipoSocio = "C";
        private const string TipoSocioOpuesto = "P";
        private const int TipoBase = 6;
        private const decimal SignoInventario = -1m;
        private const string MsgSerieOtroObjeto = "La serie no corresponde a notas de débito de cliente.";
        private const string MsgSocioTipo = "El socio de negocio debe ser un cliente.";
        private const string MsgBaseRequerida = "Indique la factura base.";
        private const string MsgBaseNoExiste = "La factura base no existe.";
        private const string MsgBaseEsNota = "La factura base no puede ser una nota de débito.";
        private const string MsgBaseCancelada = "La factura base está cancelada.";
        private const string MsgBaseOtroSocio = "La factura base pertenece a otro socio de negocio.";
        private const string MsgFacturaNoExiste = "La factura no existe.";
        private const string MsgFacturaCancelada = "La factura está cancelada.";
        // ---- fin de constantes ----

        private const string MsgModoInvalido = "Indique el tipo de nota: por artículos ('I') o por concepto ('S').";

        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<Impuesto, string>> _repoImpuesto = new();
        private readonly Mock<IRepositorioGenerico<Monedum, string>> _repoMoneda = new();
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArticulo = new();
        private readonly Mock<IRepositorioGenerico<Almacen, string>> _repoAlmacen = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly NotaDebitoDomain _domain;

        private readonly List<MovimientoRequest> _movimientos = new();

        public NotaDebitoDomainTests()
        {
            _domain = new NotaDebitoDomain(
                _repoFactura.Object, _repoDetalle.Object, _repoNumeracion.Object, _repoSocio.Object,
                _repoImpuesto.Object, _repoMoneda.Object, _repoArticulo.Object, _repoAlmacen.Object,
                _tx.Object, _asiento.Object);

            // El doble del ejecutor corre el Func directo (sin transaccion ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoFactura.Setup(r => r.InsertarAsync(It.IsAny<Factura>()))
                .ReturnsAsync((Factura n) => { n.Entry = 99; return n; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<FacturaDetalle>())).Returns(Task.CompletedTask);
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((ms, _) => _movimientos.AddRange(ms))
                .Returns(Task.CompletedTask);
            _asiento.Setup(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>())).Returns(Task.CompletedTask);

            // Valores por defecto validos: cada test rompe solo lo que quiere probar.
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie());
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", TipoSn = TipoSocio, Activo = "S" });
            _repoMoneda.Setup(r => r.ObtenerAsync("QTZ")).ReturnsAsync(new Monedum { Codigo = "QTZ" });
            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA" });
            _repoArticulo.Setup(r => r.ObtenerAsync("ART1")).ReturnsAsync(new Articulo { Codigo = "ART1" });
            _repoAlmacen.Setup(r => r.ObtenerAsync("01")).ReturnsAsync(new Almacen { Codigo = "01" });
        }

        private static NumeracionDocumentoDet CrearSerie(int? sig = 5, int? fin = null, string bloqueado = "N", string manual = "N", string? codigoObj = null, string? subTipo = null) => new()
        {
            CodigoObj = codigoObj ?? CodigoObj, Serie = 4, NombreSerie = "Primario",
            SigNumero = sig, FinNumero = fin, Bloqueado = bloqueado, Manual = manual,
            SubTipoDoc = subTipo ?? SubTipoNota, TipoSerie = "D"
        };

        // Nota por concepto ('S') por defecto; NotaArticulos() la cambia a por articulos ('I').
        private static Factura Nota(Action<Factura>? ajustar = null)
        {
            var n = new Factura
            {
                Serie = 4, CodigoSn = "SN1", MonedaDoc = "QTZ", DocType = "S",
                FechaDoc = new DateTime(2026, 10, 4), FechaEmision = new DateTime(2026, 10, 4)
            };
            ajustar?.Invoke(n);
            return n;
        }

        private static Factura NotaArticulos(Action<Factura>? ajustar = null) =>
            Nota(n => { n.DocType = "I"; ajustar?.Invoke(n); });

        private static FacturaDetalle Linea(Action<FacturaDetalle>? ajustar = null)
        {
            var l = new FacturaDetalle
            {
                Descripcion = "Cargo por flete", Cantidad = 1m, Precio = 100m, PrctjeDesc = 0m,
                CodigoImpuesto = "IVA", Impuesto = 12m, TotalLinea = 112m
            };
            ajustar?.Invoke(l);
            return l;
        }

        private static FacturaDetalle LineaArticulo(Action<FacturaDetalle>? ajustar = null)
        {
            var l = new FacturaDetalle
            {
                CodArticulo = "ART1", CodAlmacen = "01", Descripcion = "Tornillo", Cantidad = 2m, Precio = 10m,
                PrctjeDesc = 0m, CodigoImpuesto = "IVA", Impuesto = 2.4m, TotalLinea = 22.4m
            };
            ajustar?.Invoke(l);
            return l;
        }

        private async Task<string> MensajeAlInsertar(Factura nota, params FacturaDetalle[] lineas)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(nota, lineas));
            return ex.Message;
        }

        // ---------------- InsertarAsync: camino feliz por concepto ----------------

        [Fact]
        public async Task InsertarAsync_PorConcepto_NumeraGuardaEncabezadoYLineasSinInventario()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var nota = Nota();

            var entry = await _domain.InsertarAsync(nota, new[] { Linea(), Linea(l => l.Descripcion = "Intereses") });

            Assert.Equal(99, entry);
            Assert.Equal(SubTipoNota, nota.DocSubType);
            Assert.Equal(CodigoObj, nota.TipoObjeto);
            Assert.Equal("S", nota.DocType);
            Assert.Equal("C", nota.EstadoInv);          // sin inventario: explicito, no el DEFAULT 'A' de la base
            Assert.Equal(5, nota.NumDoc);
            Assert.Equal(6, serie.SigNumero);           // arrancaba en 5: avanzo
            Assert.Equal("N", nota.NumManual);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<FacturaDetalle>(l => l.Entry == 99 && l.NoLinea == 1 && l.TipoObjeto == CodigoObj)), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<FacturaDetalle>(l => l.Entry == 99 && l.NoLinea == 2 && l.Descripcion == "Intereses")), Times.Once);
            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_FuerzaLosCamposDeSistema()
        {
            var nota = Nota(n =>
            {
                n.Cancelado = "S"; n.EstadoDoc = "C"; n.FechaCancelado = DateTime.Now;
                n.TipoObjeto = "99"; n.DocSubType = "--"; n.EstadoInv = "A";
            });

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal("N", nota.Cancelado);
            Assert.Equal("A", nota.EstadoDoc);
            Assert.Null(nota.FechaCancelado);
            Assert.Equal(CodigoObj, nota.TipoObjeto);
            Assert.Equal(SubTipoNota, nota.DocSubType);
            Assert.Equal("C", nota.EstadoInv);
        }

        [Fact]
        public async Task InsertarAsync_FuerzaImprimidoEnN()
        {
            var nota = Nota(n => n.Imprimido = "S");

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal("N", nota.Imprimido);
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

        // ---------------- InsertarAsync: modo por articulos (inventario) ----------------

        [Fact]
        public async Task InsertarAsync_PorArticulos_AsientaElMovimientoYMarcaEstadoInvA()
        {
            var nota = NotaArticulos(n => n.FechaDoc = new DateTime(2026, 10, 4));

            var entry = await _domain.InsertarAsync(nota, new[]
            {
                LineaArticulo(),
                LineaArticulo(l => { l.Cantidad = 1m; l.Precio = 5m; })
            });

            Assert.Equal(99, entry);
            Assert.Equal("I", nota.DocType);
            Assert.Equal("A", nota.EstadoInv);
            Assert.Equal(2, _movimientos.Count);
            Assert.Equal((CodigoObj, 99, 1, "ART1", "01", SignoInventario * 2m, 10m), (
                _movimientos[0].TipoDoc, _movimientos[0].DocEntry, _movimientos[0].DocLinea,
                _movimientos[0].CodArticulo, _movimientos[0].CodAlmacen, _movimientos[0].Cantidad, _movimientos[0].PrecioUnitario));
            Assert.Equal((2, SignoInventario * 1m, 5m), (_movimientos[1].DocLinea, _movimientos[1].Cantidad, _movimientos[1].PrecioUnitario));
            Assert.Equal(new DateTime(2026, 10, 4), _movimientos[0].Fecha);
            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_GuardaElArticuloYAlmacenDeLaLinea()
        {
            await _domain.InsertarAsync(NotaArticulos(), new[] { LineaArticulo() });

            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<FacturaDetalle>(l =>
                l.Entry == 99 && l.NoLinea == 1 && l.CodArticulo == "ART1" && l.CodAlmacen == "01" && l.TipoObjeto == CodigoObj)), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_LaDescripcionEsOpcionalYSeRecorta()
        {
            var sinDescripcion = LineaArticulo(l => l.Descripcion = null);
            var conEspacios = LineaArticulo(l => l.Descripcion = "  Tornillo  ");

            await _domain.InsertarAsync(NotaArticulos(), new[] { sinDescripcion, conEspacios });

            Assert.Null(sinDescripcion.Descripcion);
            Assert.Equal("Tornillo", conEspacios.Descripcion);
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_SinArticulo_Lanza()
        {
            Assert.Equal("Línea 1: el artículo es requerido.",
                await MensajeAlInsertar(NotaArticulos(), LineaArticulo(l => l.CodArticulo = " ")));
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_SinAlmacen_Lanza()
        {
            Assert.Equal("Línea 1: el almacén es requerido.",
                await MensajeAlInsertar(NotaArticulos(), LineaArticulo(l => l.CodAlmacen = null)));
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_ArticuloInexistente_Lanza()
        {
            Assert.Equal("Línea 2: el artículo 'ZZZ' no existe.",
                await MensajeAlInsertar(NotaArticulos(), LineaArticulo(), LineaArticulo(l => l.CodArticulo = "ZZZ")));
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_AlmacenInexistente_Lanza()
        {
            Assert.Equal("Línea 1: el almacén 'XX' no existe.",
                await MensajeAlInsertar(NotaArticulos(), LineaArticulo(l => l.CodAlmacen = "XX")));
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_SiElAsientoFalla_PropagaElError()
        {
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .ThrowsAsync(new StockInsuficienteException("ART1", "01", 1m, 2m));

            await Assert.ThrowsAsync<StockInsuficienteException>(() =>
                _domain.InsertarAsync(NotaArticulos(), new[] { LineaArticulo() }));
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_ElAsientoVaDentroDeLaTransaccion()
        {
            // Un ejecutor que NO corre el Func: si el asiento o el guardado estuvieran fuera, se llamarian igual.
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).ReturnsAsync(0);

            await _domain.InsertarAsync(NotaArticulos(), new[] { LineaArticulo() });

            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()), Times.Never);
            _repoFactura.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
        }

        // ---------------- InsertarAsync: modo ----------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("X")]
        public async Task InsertarAsync_ModoInvalido_Lanza(string? modo)
        {
            Assert.Equal(MsgModoInvalido, await MensajeAlInsertar(Nota(n => n.DocType = modo!), Linea()));
        }

        [Theory]
        [InlineData("ART1", null)]
        [InlineData(null, "01")]
        public async Task InsertarAsync_PorConcepto_ConArticuloOAlmacen_Lanza(string? articulo, string? almacen)
        {
            Assert.Equal("Línea 1: una nota por concepto no lleva artículo ni almacén.",
                await MensajeAlInsertar(Nota(), Linea(l => { l.CodArticulo = articulo; l.CodAlmacen = almacen; })));
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
        public async Task InsertarAsync_SerieDeFacturaNormal_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(subTipo: "--"));

            Assert.Equal(MsgSerieOtroObjeto, await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(bloqueado: "S"));

            Assert.Equal("La serie está bloqueada y no se puede usar para registrar notas de débito.", await MensajeAlInsertar(Nota(), Linea()));
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
        public async Task InsertarAsync_PorConcepto_LineaSinDescripcion_Lanza(string descripcion, string esperado)
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
        public async Task InsertarAsync_ConErrorDeValidacion_NoGuardaNadaNiAvanzaLaSerie()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nota(), Array.Empty<FacturaDetalle>()));

            Assert.Equal(5, serie.SigNumero);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _repoFactura.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_ConBaseInvalida_NoAvanzaLaSerie()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = 777; }), new[] { Linea() }));

            Assert.Equal(5, serie.SigNumero);
            _repoFactura.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_ConArticuloInexistente_NoAvanzaLaSerie()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(NotaArticulos(), new[] { LineaArticulo(l => l.CodArticulo = "ZZZ") }));

            Assert.Equal(5, serie.SigNumero);
            _repoFactura.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
        }

        // ---------------- InsertarAsync: factura base ----------------

        private void PrepararFactura(int entry = 12, string socio = "SN1", string cancelado = "N", string subTipo = "--") =>
            _repoFactura.Setup(r => r.ObtenerAsync(entry)).ReturnsAsync(new Factura { Entry = entry, CodigoSn = socio, Cancelado = cancelado, DocSubType = subTipo });

        [Fact]
        public async Task InsertarAsync_ConBaseValida_ConservaLaReferencia()
        {
            PrepararFactura();
            var nota = Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = 12; });

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
            Assert.Equal(MsgBaseNoExiste, await MensajeAlInsertar(Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = 777; }), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseQueEsUnaNotaDeDebito_Lanza()
        {
            PrepararFactura(subTipo: SubTipoNota);

            Assert.Equal(MsgBaseEsNota, await MensajeAlInsertar(Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = 12; }), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseCancelada_Lanza()
        {
            PrepararFactura(cancelado: "S");

            Assert.Equal(MsgBaseCancelada, await MensajeAlInsertar(Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = 12; }), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseDeOtroSocio_Lanza()
        {
            PrepararFactura(socio: "OTRO");

            Assert.Equal(MsgBaseOtroSocio, await MensajeAlInsertar(Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = 12; }), Linea()));
        }

        // ---------------- ActualizarAsync ----------------

        private void PrepararNota(Factura nota) => _repoFactura.Setup(r => r.ObtenerAsync(nota.Entry)).ReturnsAsync(nota);

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalse()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(5)).ReturnsAsync((Factura?)null);

            Assert.False(await _domain.ActualizarAsync(5, new Factura()));
        }

        [Fact]
        public async Task ActualizarAsync_DeUnaFacturaNormal_DevuelveFalse()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = "--", Cancelado = "N" });

            Assert.False(await _domain.ActualizarAsync(5, new Factura { Cancelado = "S" }));

            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CancelarPorConcepto_MarcaCanceladoSinTocarInventarioNiComentario()
        {
            var existente = new Factura { Entry = 5, DocSubType = SubTipoNota, DocType = "S", Cancelado = "N", EstadoDoc = "A", EstadoInv = null, Comentario = "original" };
            PrepararNota(existente);

            var ok = await _domain.ActualizarAsync(5, new Factura { Cancelado = "S" });

            Assert.True(ok);
            Assert.Equal("S", existente.Cancelado);
            Assert.NotNull(existente.FechaCancelado);
            Assert.Equal("original", existente.Comentario);
            Assert.Equal("A", existente.EstadoDoc);   // EstadoDoc no cambia al cancelar
            Assert.Equal("C", existente.EstadoInv);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CancelarPorArticulos_RevierteElInventarioYMarcaEstadoInvC()
        {
            var existente = new Factura { Entry = 5, DocSubType = SubTipoNota, DocType = "I", Cancelado = "N", EstadoInv = "A", EstadoDoc = "A" };
            PrepararNota(existente);

            var ok = await _domain.ActualizarAsync(5, new Factura { Cancelado = "S" });

            Assert.True(ok);
            Assert.Equal("S", existente.Cancelado);
            Assert.Equal("C", existente.EstadoInv);
            Assert.NotNull(existente.FechaCancelado);
            _asiento.Verify(a => a.RevertirAsync(CodigoObj, 5), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_CancelarConComentario_ActualizaElComentario()
        {
            var existente = new Factura { Entry = 5, DocSubType = SubTipoNota, DocType = "S", Cancelado = "N", Comentario = "original" };
            PrepararNota(existente);

            await _domain.ActualizarAsync(5, new Factura { Cancelado = "S", Comentario = "motivo" });

            Assert.Equal("motivo", existente.Comentario);
        }

        [Fact]
        public async Task ActualizarAsync_SoloComentario_NoCancelaNiCambiaOtrosCampos()
        {
            var existente = new Factura { Entry = 5, DocSubType = SubTipoNota, DocType = "I", Cancelado = "N", Comentario = "viejo", TotalDoc = 50m, CodigoSn = "SN1" };
            PrepararNota(existente);

            await _domain.ActualizarAsync(5, new Factura { Cancelado = "N", Comentario = "nuevo", TotalDoc = 999m, CodigoSn = "OTRO" });

            Assert.Equal("nuevo", existente.Comentario);
            Assert.Equal("N", existente.Cancelado);
            Assert.Equal(50m, existente.TotalDoc);
            Assert.Equal("SN1", existente.CodigoSn);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_DocumentoCancelado_NoSePuedeModificar()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = SubTipoNota, Cancelado = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, new Factura { Comentario = "x" }));

            Assert.Equal("El documento está cancelado y no se puede modificar.", ex.Message);
        }

        // ---------------- EliminarAsync ----------------

        [Fact]
        public async Task EliminarAsync_Inexistente_DevuelveFalse()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(5)).ReturnsAsync((Factura?)null);

            Assert.False(await _domain.EliminarAsync(5));
        }

        [Fact]
        public async Task EliminarAsync_DeUnaFacturaNormal_DevuelveFalse_SinBorrar()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = "--", Cancelado = "S" });

            Assert.False(await _domain.EliminarAsync(5));

            _repoFactura.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_SinCancelar_Lanza()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = SubTipoNota, Cancelado = "N" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(5));

            Assert.Equal("Cancele el documento (Cancelado='S') antes de eliminarlo.", ex.Message);
            _repoFactura.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_Cancelado_BorraLasLineasYElEncabezado()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = SubTipoNota, Cancelado = "S" });
            var lineas = new List<FacturaDetalle>
            {
                new() { Entry = 5, NoLinea = 1 }, new() { Entry = 5, NoLinea = 2 }, new() { Entry = 6, NoLinea = 1 }
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).ReturnsAsync(true);
            _repoFactura.Setup(r => r.EliminarAsync(5)).ReturnsAsync(true);

            var ok = await _domain.EliminarAsync(5);

            Assert.True(ok);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 5 && k.NoLinea == 1)), Times.Once);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 5 && k.NoLinea == 2)), Times.Once);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 6 && k.NoLinea == 1)), Times.Never);
            _repoFactura.Verify(r => r.EliminarAsync(5), Times.Once);
        }

        // ---------------- Consultas ----------------

        [Fact]
        public async Task ObtenerAsync_DeUnaNota_LaDevuelve()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = SubTipoNota, NumDoc = 9 });

            Assert.Equal(9, (await _domain.ObtenerAsync(5))!.NumDoc);
        }

        [Fact]
        public async Task ObtenerAsync_DeUnaFacturaNormal_DevuelveNull()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = "--" });

            Assert.Null(await _domain.ObtenerAsync(5));
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveSoloLasNotas()
        {
            var filas = new List<Factura>
            {
                new() { Entry = 1, DocSubType = "--" }, new() { Entry = 2, DocSubType = SubTipoNota }, new() { Entry = 3, DocSubType = SubTipoNota }
            };
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());

            var resultado = (await _domain.ObtenerTodoAsync()).ToList();

            Assert.Equal(new[] { 2, 3 }, resultado.Select(n => n.Entry));
        }

        [Fact]
        public async Task ObtenerLineasAsync_DevuelveSoloLasDeLaNotaEnOrden()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = SubTipoNota });
            var lineas = new List<FacturaDetalle>
            {
                new() { Entry = 5, NoLinea = 2 }, new() { Entry = 6, NoLinea = 1 }, new() { Entry = 5, NoLinea = 1 }
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());

            var resultado = (await _domain.ObtenerLineasAsync(5)).ToList();

            Assert.Equal(new[] { 1, 2 }, resultado.Select(l => l.NoLinea));
        }

        [Fact]
        public async Task ObtenerLineasAsync_DeUnaFacturaNormal_DevuelveVacio()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = "--" });
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaDetalle> { new() { Entry = 5, NoLinea = 1 } }.AsAsyncQueryable());

            Assert.Empty(await _domain.ObtenerLineasAsync(5));
        }

        [Fact]
        public async Task ObtenerFacturasBaseAsync_DevuelveLasFacturasNormalesNoCanceladasDelSocioDeLaMasNuevaALaMasVieja()
        {
            var facturas = new List<Factura>
            {
                new() { Entry = 1, CodigoSn = "SN1", Cancelado = "N" },
                new() { Entry = 2, CodigoSn = "SN1", Cancelado = "S" },
                new() { Entry = 3, CodigoSn = "OTRO", Cancelado = "N" },
                new() { Entry = 4, CodigoSn = "SN1", Cancelado = "N" },
                new() { Entry = 5, CodigoSn = "SN1", Cancelado = "N", DocSubType = SubTipoNota }   // una nota no es factura base
            };
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());

            var resultado = (await _domain.ObtenerFacturasBaseAsync("SN1")).ToList();

            Assert.Equal(new[] { 4, 1 }, resultado.Select(f => f.Entry));
        }

        [Fact]
        public async Task ObtenerFacturaBaseAsync_DevuelveEncabezadoYLineasOrdenadas()
        {
            PrepararFactura(entry: 12);
            var lineas = new List<FacturaDetalle>
            {
                new() { Entry = 12, NoLinea = 2 }, new() { Entry = 13, NoLinea = 1 }, new() { Entry = 12, NoLinea = 1 }
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());

            var resultado = await _domain.ObtenerFacturaBaseAsync(12);

            Assert.Equal(12, resultado.Documento.Entry);
            Assert.Equal(new[] { 1, 2 }, resultado.Lineas.Select(l => l.NoLinea));
        }

        [Fact]
        public async Task ObtenerFacturaBaseAsync_Inexistente_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ObtenerFacturaBaseAsync(777));

            Assert.Equal(MsgFacturaNoExiste, ex.Message);
        }

        [Fact]
        public async Task ObtenerFacturaBaseAsync_DeUnaNota_LanzaNoExiste()
        {
            PrepararFactura(entry: 12, subTipo: SubTipoNota);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ObtenerFacturaBaseAsync(12));

            Assert.Equal(MsgFacturaNoExiste, ex.Message);
        }

        [Fact]
        public async Task ObtenerFacturaBaseAsync_Cancelada_Lanza()
        {
            PrepararFactura(entry: 12, cancelado: "S");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ObtenerFacturaBaseAsync(12));

            Assert.Equal(MsgFacturaCancelada, ex.Message);
        }
    }
}
