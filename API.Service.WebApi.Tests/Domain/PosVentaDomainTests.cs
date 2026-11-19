using System.Data;
using API.Domain.Core.Pos;
using API.Domain.Core.Reportes.Compartido;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Pos;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>Venta atómica del POS, ticket y ventas del día (dominios de factura y pago simulados).</summary>
    public class PosVentaDomainTests
    {
        /// <summary>Ejecutor falso: corre la operación, propaga excepciones y recuerda el aislamiento pedido.</summary>
        private sealed class EjecutorFalso : IEjecutorTransaccion
        {
            public int Llamadas { get; private set; }
            public IsolationLevel? Nivel { get; private set; }
            public bool Dentro { get; private set; }

            public Task<T> EjecutarAsync<T>(Func<Task<T>> operacion) => Ejecutar(operacion, null);

            public Task<T> EjecutarAsync<T>(Func<Task<T>> operacion, IsolationLevel nivel) => Ejecutar(operacion, nivel);

            private async Task<T> Ejecutar<T>(Func<Task<T>> operacion, IsolationLevel? nivel)
            {
                Llamadas++;
                Nivel = nivel;
                Dentro = true;
                try { return await operacion(); }
                finally { Dentro = false; }
            }
        }

        private readonly Mock<IRepositorioGenerico<PosConfiguracion, int>> _repoConfig = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoSerie = new();
        private readonly Mock<IRepositorioGenerico<Impuesto, string>> _repoImpuesto = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<FormaPago, string>> _repoFormaPago = new();
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArticulo = new();
        private readonly Mock<IPrecioListaDomain> _precios = new();
        private readonly Mock<IFacturaDomain> _facturaDomain = new();
        private readonly Mock<IPagoRecibidoDomain> _pagoDomain = new();
        private readonly EjecutorFalso _tx = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibido, int>> _repoPago = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)>> _repoPagoFactura = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)>> _repoMedio = new();
        private readonly PosDomain _domain;

        private readonly Dictionary<string, decimal> _precioLista = new() { ["A1"] = 45m, ["A2"] = 22m, ["Z0"] = 0m };

        private Factura? _factura;
        private List<FacturaDetalle>? _lineas;
        private PagoRecibido? _pago;
        private List<PagoRecibidoFactura>? _pagoFacturas;
        private List<PagoRecibidoMedio>? _medios;
        private bool _facturaDentroDeTx, _pagoDentroDeTx;
        private readonly List<int?> _listasConsultadas = new();

        public PosVentaDomainTests()
        {
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new PosConfiguracion
            {
                Id = 1, SerieFactura = 10, SeriePago = 20, CodAlmacen = "ALM1", CodigoImpuesto = "IVA",
                ClienteDefecto = "CF1", FormaPagoEfectivo = "EFE", MonedaDoc = "GTQ"
            });
            _repoSerie.Setup(r => r.ObtenerAsync(10)).ReturnsAsync(new NumeracionDocumentoDet { Serie = 10, CodigoObj = "6", SubTipoDoc = "--", NombreSerie = "POS", TipoSerie = "A" });
            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA", Nombre = "IVA", Tasa = 12m });
            _repoSocio.Setup(r => r.ObtenerAsync("CF1")).ReturnsAsync(new SocioNegocio { Codigo = "CF1", Nombre = "Consumidor Final", TipoSn = "C", Activo = "S", NumLstPrecio = 1 });
            _repoSocio.Setup(r => r.ObtenerAsync("C2")).ReturnsAsync(new SocioNegocio { Codigo = "C2", Nombre = "Juan", TipoSn = "C", Activo = "S", Nit = "1234567-8", NumLstPrecio = 3 });
            _repoSocio.Setup(r => r.ObtenerAsync("P1")).ReturnsAsync(new SocioNegocio { Codigo = "P1", Nombre = "Proveedor", TipoSn = "P", Activo = "S" });
            _repoSocio.Setup(r => r.ObtenerAsync("CIN")).ReturnsAsync(new SocioNegocio { Codigo = "CIN", Nombre = "Inactivo", TipoSn = "C", Activo = "N" });

            _repoFormaPago.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                new FormaPago { Codigo = "EFE", Nombre = "Efectivo", TipoDato = "N", Activo = "S" },
                new FormaPago { Codigo = "TAR", Nombre = "Tarjeta", TipoDato = "J", Activo = "S" },
                new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "S" },
                new FormaPago { Codigo = "VIE", Nombre = "Vieja", TipoDato = "N", Activo = "N" }
            }.AsAsyncQueryable());

            _repoArticulo.Setup(r => r.ObtenerAsync("A1")).ReturnsAsync(Art("A1", "Aceite"));
            _repoArticulo.Setup(r => r.ObtenerAsync("A2")).ReturnsAsync(Art("A2", "Bujía"));
            _repoArticulo.Setup(r => r.ObtenerAsync("Z0")).ReturnsAsync(Art("Z0", "Sin precio"));
            _repoArticulo.Setup(r => r.ObtenerAsync("IN")).ReturnsAsync(Art("IN", "Inactivo", activo: "N"));
            _repoArticulo.Setup(r => r.ObtenerAsync("NV")).ReturnsAsync(Art("NV", "No de venta", venta: "N"));

            _precios.Setup(p => p.ResolverAsync(It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync((string codigo, int? lista) =>
                {
                    _listasConsultadas.Add(lista);
                    var p = _precioLista.TryGetValue(codigo, out var v) ? v : 0m;
                    return new PrecioResuelto(codigo, p, 1m, p);
                });

            _facturaDomain.Setup(d => d.InsertarAsync(It.IsAny<Factura>(), It.IsAny<IEnumerable<FacturaDetalle>>()))
                .Callback((Factura f, IEnumerable<FacturaDetalle> l) =>
                {
                    _facturaDentroDeTx = _tx.Dentro;
                    _factura = f;
                    _lineas = l.ToList();
                    f.NumDoc = 77;
                })
                .ReturnsAsync(501);

            _pagoDomain.Setup(d => d.InsertarAsync(It.IsAny<PagoRecibido>(), It.IsAny<IEnumerable<PagoRecibidoFactura>>(), It.IsAny<IEnumerable<PagoRecibidoMedio>>()))
                .Callback((PagoRecibido p, IEnumerable<PagoRecibidoFactura> f, IEnumerable<PagoRecibidoMedio> m) =>
                {
                    _pagoDentroDeTx = _tx.Dentro;
                    _pago = p;
                    _pagoFacturas = f.ToList();
                    _medios = m.ToList();
                })
                .ReturnsAsync(900);

            var vacioFacturas = new Mock<IRepositorioGenerico<Factura, int>>();
            vacioFacturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(Array.Empty<Factura>().AsAsyncQueryable());

            _domain = new PosDomain(_repoConfig.Object, _repoSerie.Object, new Mock<IRepositorioGenerico<Almacen, string>>().Object,
                _repoImpuesto.Object, _repoSocio.Object, _repoFormaPago.Object, new Mock<IRepositorioGenerico<Monedum, string>>().Object,
                _repoArticulo.Object,
                new Mock<IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>>().Object,
                _precios.Object,
                new ConsultaLineasVenta(vacioFacturas.Object, new Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>>().Object,
                    new Mock<IRepositorioGenerico<MovimientoInventario, int>>().Object),
                _facturaDomain.Object, _pagoDomain.Object, _tx,
                _repoFactura.Object, _repoDetalle.Object, _repoPago.Object, _repoPagoFactura.Object, _repoMedio.Object);
        }

        private static Articulo Art(string codigo, string nombre, string activo = "S", string venta = "S") => new()
        {
            Codigo = codigo, Nombre = nombre, Activo = activo, ArticuloVenta = venta, ArticuloInventario = "S"
        };

        private static VentaPosSolicitud Solicitud(string? cliente, string? nit, (string Cod, decimal Cant)[] lineas, params (string Forma, decimal Monto)[] pagos) =>
            new(cliente, nit,
                lineas.Select(l => new VentaPosLinea(l.Cod, l.Cant)).ToList(),
                pagos.Select(p => new VentaPosPago(p.Forma, p.Monto, null, null, null)).ToList());

        private static VentaPosSolicitud Feliz(string? cliente = null, string? nit = null) =>
            Solicitud(cliente, nit, new[] { ("A1", 2m), ("A2", 1m) }, ("EFE", 200m));

        private void NadaRegistrado()
        {
            _facturaDomain.Verify(d => d.InsertarAsync(It.IsAny<Factura>(), It.IsAny<IEnumerable<FacturaDetalle>>()), Times.Never);
            _pagoDomain.Verify(d => d.InsertarAsync(It.IsAny<PagoRecibido>(), It.IsAny<IEnumerable<PagoRecibidoFactura>>(), It.IsAny<IEnumerable<PagoRecibidoMedio>>()), Times.Never);
            Assert.Equal(0, _tx.Llamadas);
        }

        // ---------- Venta feliz ----------

        [Fact]
        public async Task Vender_Feliz_RegistraFacturaYPagoDentroDeUnaTransaccionSerializable_YDevuelveElTicket()
        {
            var ticket = await _domain.VenderAsync(Feliz(), "ana");

            // Transacción exterior única, Serializable (la pide el pago y rige la del exterior).
            Assert.Equal(1, _tx.Llamadas);
            Assert.Equal(IsolationLevel.Serializable, _tx.Nivel);
            Assert.True(_facturaDentroDeTx);
            Assert.True(_pagoDentroDeTx);

            // Factura
            var f = Assert.IsType<Factura>(_factura);
            Assert.Equal(10, f.Serie);
            Assert.Equal("CF1", f.CodigoSn);
            Assert.Equal("Consumidor Final", f.NombreSn);
            Assert.Equal(DateTime.Today, f.FechaDoc);
            Assert.Equal(DateTime.Today, f.FechaEmision!.Value.Date);
            Assert.Equal("GTQ", f.MonedaDoc);
            Assert.Equal("CF", f.Nit);
            Assert.Equal("CON", f.CondicionPago);
            Assert.Equal("Venta POS - Cajero: ana", f.Comentario);
            Assert.Equal(12m, f.PrctjeImpuesto);
            Assert.Equal(0m, f.PrctjeDesc);
            Assert.Equal(112.00m, f.TotalBruto);
            Assert.Equal(0m, f.TotalDesc);
            Assert.Equal(13.44m, f.TotalImp);
            Assert.Equal(125.44m, f.TotalDoc);
            Assert.Equal("A", f.EstadoDoc);
            Assert.Equal(125.44m, f.PagoInmediato);

            var lineas = Assert.IsType<List<FacturaDetalle>>(_lineas);
            Assert.Equal(2, lineas.Count);
            Assert.All(lineas, l =>
            {
                Assert.Equal("ALM1", l.CodAlmacen);
                Assert.Null(l.UomEntry);
                Assert.Null(l.BaseEntry);
                Assert.Equal("IVA", l.CodigoImpuesto);
                Assert.Equal("6", l.TipoObjeto);
                Assert.Equal(0m, l.PrctjeDesc);
            });
            Assert.Equal(("A1", "Aceite", 2m, 45m, 45m, 10.80m, 100.80m),
                (lineas[0].CodArticulo, lineas[0].Descripcion, lineas[0].Cantidad, lineas[0].Precio, lineas[0].PrecioBruto, lineas[0].Impuesto, lineas[0].TotalLinea));
            Assert.Equal(("A2", "Bujía", 1m, 22m, 2.64m, 24.64m),
                (lineas[1].CodArticulo, lineas[1].Descripcion, lineas[1].Cantidad, lineas[1].Precio, lineas[1].Impuesto, lineas[1].TotalLinea));

            // Pago: exactamente el total; el cambio sale del efectivo.
            var p = Assert.IsType<PagoRecibido>(_pago);
            Assert.Equal(20, p.Serie);
            Assert.Equal("CF1", p.CodigoSn);
            Assert.Equal("Consumidor Final", p.NombreSn);
            Assert.Equal(DateTime.Today, p.FechaDoc!.Value.Date);
            Assert.Equal("Venta POS - Cajero: ana", p.Comentario);
            var aplicado = Assert.Single(_pagoFacturas!);
            Assert.Equal((501, 125.44m), (aplicado.FacturaEntry, aplicado.MontoAplicado));
            var medio = Assert.Single(_medios!);
            Assert.Equal(("EFE", 125.44m), (medio.CodFormaPago, medio.Monto));

            // Ticket
            Assert.Equal(501, ticket.FacturaEntry);
            Assert.Equal(10, ticket.Serie);
            Assert.Equal("POS", ticket.NombreSerie);
            Assert.Equal(77, ticket.NumDoc);
            Assert.Equal("ana", ticket.Cajero);
            Assert.Equal(("CF1", "Consumidor Final", "CF"), (ticket.CodigoCliente, ticket.NombreCliente, ticket.Nit));
            Assert.Equal(2, ticket.Lineas.Count);
            Assert.Equal(new TicketPosLinea("A1", "Aceite", 2m, 45m, 100.80m), ticket.Lineas[0]);
            Assert.Equal((112.00m, 13.44m, 125.44m), (ticket.Subtotal, ticket.Impuesto, ticket.Total));
            Assert.Equal(200.00m, ticket.Pagado);
            Assert.Equal(74.56m, ticket.Cambio);
            var pagoTicket = Assert.Single(ticket.Pagos);
            Assert.Equal(new TicketPosPago("EFE", "Efectivo", 200m), pagoTicket);
            Assert.Equal("GTQ", ticket.Moneda);
            Assert.Equal(string.Empty, ticket.Empresa);
        }

        [Theory]
        [InlineData(null, null, "CF")]           // ni solicitado ni del socio
        [InlineData("C2", null, "1234567-8")]    // del socio
        [InlineData("C2", "  ", "1234567-8")]    // vacío = del socio
        [InlineData("C2", " 999 ", "999")]       // el solicitado, recortado
        public async Task Vender_Nit_SolicitadoLuegoElDelSocioLuegoCF(string? cliente, string? nit, string esperado)
        {
            var ticket = await _domain.VenderAsync(Feliz(cliente, nit), "ana");

            Assert.Equal(esperado, _factura!.Nit);
            Assert.Equal(esperado, ticket.Nit);
        }

        [Fact]
        public async Task Vender_ElPrecioSaleDeLaListaDelCliente()
        {
            await _domain.VenderAsync(Feliz("C2"), "ana");

            // La solicitud no trae precio: se resuelve con la lista del cliente (3).
            Assert.All(_listasConsultadas, l => Assert.Equal(3, l));
            _precios.Verify(p => p.ResolverAsync("A1", 3), Times.Once);
            _precios.Verify(p => p.ResolverAsync("A2", 3), Times.Once);
            Assert.Equal(45m, _lineas![0].Precio);
        }

        [Fact]
        public async Task Vender_ArticuloRepetido_SeSumaEnUnaLinea_SinDistinguirMayusculas()
        {
            var ticket = await _domain.VenderAsync(
                Solicitud(null, null, new[] { ("A1", 1m), ("A2", 1m), ("a1", 2m) }, ("EFE", 500m)), "ana");

            Assert.Equal(2, _lineas!.Count);
            Assert.Equal(("A1", 3m), (_lineas[0].CodArticulo, _lineas[0].Cantidad));
            Assert.Equal("A2", _lineas[1].CodArticulo);
            Assert.Equal(135m + 22m, _factura!.TotalBruto);
            Assert.Equal(3m, ticket.Lineas[0].Cantidad);
        }

        [Fact]
        public async Task Vender_FormaDePago_SeNormalizaContraElCatalogo()
        {
            await _domain.VenderAsync(Solicitud(null, null, new[] { ("A1", 2m), ("A2", 1m) }, (" efe ", 200m)), "ana");

            // Con el código canónico el cambio sí sale del efectivo de la configuración.
            Assert.Equal(("EFE", 125.44m), (_medios![0].CodFormaPago, _medios[0].Monto));
        }

        [Fact]
        public async Task Vender_PagoMixto_TarjetaYEfectivoConCambio()
        {
            var s = new VentaPosSolicitud(null, null, new() { new("A1", 2m), new("A2", 1m) },
                new() { new("TAR", 100m, " 4321 ", null, null), new("EFE", 50m, null, null, null) });

            var ticket = await _domain.VenderAsync(s, "ana");

            Assert.Equal(2, _medios!.Count);
            Assert.Equal(("TAR", 100m, "4321"), (_medios[0].CodFormaPago, _medios[0].Monto, _medios[0].Referencia));
            Assert.Equal(("EFE", 25.44m), (_medios[1].CodFormaPago, _medios[1].Monto));
            Assert.Equal((150m, 24.56m), (ticket.Pagado, ticket.Cambio));
        }

        // ---------- Validaciones: nada se registra ----------

        public static IEnumerable<object[]> Rechazos()
        {
            var ok = new[] { ("A1", 2m), ("A2", 1m) };
            yield return new object[] { Solicitud("NOEXISTE", null, ok, ("EFE", 200m)), "El cliente no existe o no es un cliente." };
            yield return new object[] { Solicitud("P1", null, ok, ("EFE", 200m)), "El cliente no existe o no es un cliente." };
            yield return new object[] { Solicitud("CIN", null, ok, ("EFE", 200m)), "El cliente no existe o no es un cliente." };
            yield return new object[] { Solicitud(null, null, Array.Empty<(string, decimal)>(), ("EFE", 200m)), "Agrega al menos un artículo a la venta." };
            yield return new object[] { Solicitud(null, null, new[] { ("A1", 0m) }, ("EFE", 200m)), "La cantidad de A1 debe ser mayor que cero." };
            yield return new object[] { Solicitud(null, null, new[] { ("A1", 1m), ("A2", -1m) }, ("EFE", 200m)), "La cantidad de A2 debe ser mayor que cero." };
            yield return new object[] { Solicitud(null, null, new[] { ("NOEX", 1m) }, ("EFE", 200m)), "El artículo NOEX no se puede vender en el POS." };
            yield return new object[] { Solicitud(null, null, new[] { ("IN", 1m) }, ("EFE", 200m)), "El artículo IN no se puede vender en el POS." };
            yield return new object[] { Solicitud(null, null, new[] { ("NV", 1m) }, ("EFE", 200m)), "El artículo NV no se puede vender en el POS." };
            yield return new object[] { Solicitud(null, null, new[] { ("Z0", 1m) }, ("EFE", 200m)), "El artículo Z0 no se puede vender en el POS." };
            yield return new object[] { Solicitud(null, null, new[] { ("A1", 0.0001m) }, ("EFE", 200m)), "El total de la venta debe ser mayor que cero." };
            yield return new object[] { Solicitud(null, null, ok, ("EFE", 100m)), "El pago (100.00) no cubre el total (125.44)." };
            yield return new object[] { Solicitud(null, null, ok, ("TAR", 200m)), "El cambio (74.56) solo puede darse con efectivo." };
            yield return new object[] { Solicitud(null, null, ok), "Indica al menos una forma de pago." };
            yield return new object[] { Solicitud(null, null, ok, ("XYZ", 200m)), "La forma de pago XYZ no existe o está inactiva." };
            yield return new object[] { Solicitud(null, null, ok, ("vie", 200m)), "La forma de pago vie no existe o está inactiva." };
            yield return new object[] { Solicitud(null, null, ok, ("CHQ", 125.44m)), "La forma de pago Cheque requiere el número de cheque." };
            yield return new object[] { Solicitud(null, "1234567890123456", ok, ("EFE", 200m)), "El NIT no puede tener más de 15 caracteres." };
        }

        [Theory]
        [MemberData(nameof(Rechazos))]
        public async Task Vender_Rechazo_LanzaElMensaje_YNoRegistraNada(VentaPosSolicitud solicitud, string mensaje)
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(() => _domain.VenderAsync(solicitud, "ana"));

            Assert.Equal(mensaje, ex.Message);
            NadaRegistrado();
        }

        [Fact]
        public async Task Vender_SinConfiguracion_LanzaConLosFaltantes_YNoRegistraNada()
        {
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new PosConfiguracion { Id = 1, SerieFactura = 10, CodAlmacen = "ALM1", CodigoImpuesto = "IVA", ClienteDefecto = "CF1", MonedaDoc = "GTQ" });

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => _domain.VenderAsync(Feliz(), "ana"));

            Assert.Equal("El POS no está configurado: falta Serie de pagos, Forma de pago en efectivo.", ex.Message);
            NadaRegistrado();
        }

        [Fact]
        public async Task Vender_SinFilaDeConfiguracion_LanzaConTodosLosFaltantes()
        {
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync((PosConfiguracion?)null);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => _domain.VenderAsync(Feliz(), "ana"));

            Assert.StartsWith("El POS no está configurado: falta Serie de factura POS, Serie de pagos,", ex.Message);
            NadaRegistrado();
        }

        // ---------- Todo o nada ----------

        [Fact]
        public async Task Vender_FallaElPago_LaExcepcionSaleDeVender_SinTicket()
        {
            _pagoDomain.Setup(d => d.InsertarAsync(It.IsAny<PagoRecibido>(), It.IsAny<IEnumerable<PagoRecibidoFactura>>(), It.IsAny<IEnumerable<PagoRecibidoMedio>>()))
                .ThrowsAsync(new Exception("Factura 77 está cancelada."));

            TicketPos? ticket = null;
            var ex = await Assert.ThrowsAnyAsync<Exception>(async () => ticket = await _domain.VenderAsync(Feliz(), "ana"));

            Assert.Equal("Factura 77 está cancelada.", ex.Message);
            Assert.Null(ticket);
            Assert.Equal(1, _tx.Llamadas);   // la factura corrió dentro de la misma transacción exterior, que se revierte
            _facturaDomain.Verify(d => d.InsertarAsync(It.IsAny<Factura>(), It.IsAny<IEnumerable<FacturaDetalle>>()), Times.Once);
        }

        [Fact]
        public async Task Vender_FallaLaFactura_NoSeIntentaElPago()
        {
            _facturaDomain.Setup(d => d.InsertarAsync(It.IsAny<Factura>(), It.IsAny<IEnumerable<FacturaDetalle>>()))
                .ThrowsAsync(new Exception("Stock insuficiente."));

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => _domain.VenderAsync(Feliz(), "ana"));

            Assert.Equal("Stock insuficiente.", ex.Message);
            _pagoDomain.Verify(d => d.InsertarAsync(It.IsAny<PagoRecibido>(), It.IsAny<IEnumerable<PagoRecibidoFactura>>(), It.IsAny<IEnumerable<PagoRecibidoMedio>>()), Times.Never);
        }

        // ---------- Ticket ----------

        private static Factura FacturaPos(int entry, string? comentario = "Venta POS - Cajero: ana", string cancelado = "N", string subTipo = "--", DateTime? fecha = null) => new()
        {
            Entry = entry, Serie = 10, NumDoc = 77, DocSubType = subTipo, Cancelado = cancelado,
            FechaDoc = (fecha ?? DateTime.Today).Date, FechaEmision = (fecha ?? DateTime.Today).Date.AddHours(10).AddMinutes(30),
            CodigoSn = "C2", NombreSn = "Juan", Nit = "1234567-8", MonedaDoc = "GTQ", Comentario = comentario,
            TotalBruto = 112m, TotalImp = 13.44m, TotalDoc = 125.44m
        };

        [Fact]
        public async Task Ticket_DeUnaVentaPos_ReconstruyeLineasMediosYCajero()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(501)).ReturnsAsync(FacturaPos(501));
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                new FacturaDetalle { Entry = 501, NoLinea = 2, CodArticulo = "A2", Descripcion = "Bujía", Cantidad = 1m, Precio = 22m, TotalLinea = 24.64m },
                new FacturaDetalle { Entry = 501, NoLinea = 1, CodArticulo = "A1", Descripcion = "Aceite", Cantidad = 2m, Precio = 45m, TotalLinea = 100.80m },
                new FacturaDetalle { Entry = 502, NoLinea = 1, CodArticulo = "A9", Descripcion = "Otro", Cantidad = 1m, Precio = 1m, TotalLinea = 1m }
            }.AsAsyncQueryable());
            _repoPagoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                new PagoRecibidoFactura { Entry = 900, NoLinea = 1, FacturaEntry = 501, MontoAplicado = 125.44m },
                new PagoRecibidoFactura { Entry = 901, NoLinea = 1, FacturaEntry = 501, MontoAplicado = 125.44m },
                new PagoRecibidoFactura { Entry = 902, NoLinea = 1, FacturaEntry = 600, MontoAplicado = 5m }
            }.AsAsyncQueryable());
            _repoPago.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                new PagoRecibido { Entry = 900, Cancelado = "N" },
                new PagoRecibido { Entry = 901, Cancelado = "S" },
                new PagoRecibido { Entry = 902, Cancelado = "N" }
            }.AsAsyncQueryable());
            _repoMedio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                new PagoRecibidoMedio { Entry = 900, NoLinea = 1, CodFormaPago = "TAR", Monto = 100m },
                new PagoRecibidoMedio { Entry = 900, NoLinea = 2, CodFormaPago = "EFE", Monto = 25.44m },
                new PagoRecibidoMedio { Entry = 901, NoLinea = 1, CodFormaPago = "EFE", Monto = 125.44m },
                new PagoRecibidoMedio { Entry = 902, NoLinea = 1, CodFormaPago = "EFE", Monto = 5m }
            }.AsAsyncQueryable());

            var t = await _domain.ObtenerTicketAsync(501);

            Assert.Equal((501, 10, "POS", 77), (t.FacturaEntry, t.Serie, t.NombreSerie, t.NumDoc));
            Assert.Equal(DateTime.Today.AddHours(10).AddMinutes(30), t.Fecha);
            Assert.Equal("ana", t.Cajero);
            Assert.Equal(("C2", "Juan", "1234567-8"), (t.CodigoCliente, t.NombreCliente, t.Nit));
            Assert.Equal(new[]
            {
                new TicketPosLinea("A1", "Aceite", 2m, 45m, 100.80m),
                new TicketPosLinea("A2", "Bujía", 1m, 22m, 24.64m)
            }, t.Lineas);
            Assert.Equal((112m, 13.44m, 125.44m), (t.Subtotal, t.Impuesto, t.Total));
            Assert.Equal(new[] { new TicketPosPago("TAR", "Tarjeta", 100m), new TicketPosPago("EFE", "Efectivo", 25.44m) }, t.Pagos);
            Assert.Equal((125.44m, 0m), (t.Pagado, t.Cambio));
            Assert.Equal("GTQ", t.Moneda);
            Assert.Equal(string.Empty, t.Empresa);
        }

        [Fact]
        public async Task Ticket_DeUnaVentaCancelada_SeConsultaConSuTotal()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(501)).ReturnsAsync(FacturaPos(501, cancelado: "S"));
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(Array.Empty<FacturaDetalle>().AsAsyncQueryable());
            _repoPagoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(Array.Empty<PagoRecibidoFactura>().AsAsyncQueryable());
            _repoPago.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(Array.Empty<PagoRecibido>().AsAsyncQueryable());
            _repoMedio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(Array.Empty<PagoRecibidoMedio>().AsAsyncQueryable());

            var t = await _domain.ObtenerTicketAsync(501);

            Assert.Equal(125.44m, t.Total);
            Assert.Empty(t.Pagos);
            Assert.Equal(0m, t.Pagado);
        }

        [Theory]
        [InlineData("Factura normal")]
        [InlineData(null)]
        [InlineData("venta pos - cajero: ana")]   // la marca distingue mayúsculas
        public async Task Ticket_DeUnaFacturaSinLaMarcaPos_LaVentaNoExiste(string? comentario)
        {
            _repoFactura.Setup(r => r.ObtenerAsync(501)).ReturnsAsync(FacturaPos(501, comentario));

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => _domain.ObtenerTicketAsync(501));

            Assert.Equal("La venta no existe.", ex.Message);
        }

        [Fact]
        public async Task Ticket_Inexistente_O_NotaDeDebito_LaVentaNoExiste()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(501)).ReturnsAsync((Factura?)null);
            _repoFactura.Setup(r => r.ObtenerAsync(502)).ReturnsAsync(FacturaPos(502, subTipo: "ND"));

            Assert.Equal("La venta no existe.", (await Assert.ThrowsAnyAsync<Exception>(() => _domain.ObtenerTicketAsync(501))).Message);
            Assert.Equal("La venta no existe.", (await Assert.ThrowsAnyAsync<Exception>(() => _domain.ObtenerTicketAsync(502))).Message);
        }

        // ---------- Ventas del día ----------

        [Fact]
        public async Task VentasDelDia_SoloMarcadasDeHoy_MasRecientesPrimero()
        {
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                FacturaPos(1),
                FacturaPos(3, cancelado: "S"),
                FacturaPos(2),
                FacturaPos(4, comentario: "Factura normal"),
                FacturaPos(5, fecha: DateTime.Today.AddDays(-1)),
                FacturaPos(6, subTipo: "ND"),
                FacturaPos(7, comentario: null),
                FacturaPos(8, fecha: DateTime.Today.AddDays(1))
            }.AsAsyncQueryable());

            var r = await _domain.ObtenerVentasDelDiaAsync();

            Assert.Equal(new[] { 3, 2, 1 }, r.Select(v => v.FacturaEntry).ToArray());
            Assert.True(r[0].Cancelada);
            Assert.False(r[1].Cancelada);
            Assert.Equal(new VentaPosDelDia(2, 10, 77, DateTime.Today.AddHours(10).AddMinutes(30), "C2", "Juan", 125.44m, false), r[1]);
        }

        [Fact]
        public async Task VentasDelDia_Maximo200()
        {
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(
                Enumerable.Range(1, 250).Select(i => FacturaPos(i)).ToArray().AsAsyncQueryable());

            var r = await _domain.ObtenerVentasDelDiaAsync();

            Assert.Equal(200, r.Count);
            Assert.Equal(250, r[0].FacturaEntry);
        }
    }
}
