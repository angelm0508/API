using System.Data;
using API.Domain.Core;
using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>Control del límite de crédito en FacturaDomain.InsertarAsync (incluye el pago inmediato).</summary>
    public class FacturaDomainCreditoTests
    {
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<AutorizacionCredito, int>> _repoAutorizacion = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly Mock<ISaldoFacturaService> _saldos = new();
        private readonly Mock<IResolutorVencimiento> _resolutor = new();
        private readonly Mock<ICreditoClienteService> _credito = new();
        private readonly AutorizacionCreditoContexto _contexto = new();
        private readonly NumeracionDocumentoDet _serie;
        private readonly List<AutorizacionCredito> _autorizaciones = new();
        private readonly FacturaDomain _domain;

        private static readonly PosicionCredito Posicion = new("CL-1", 1000m, false, 600m, 0m, 300m, 900m, 100m);
        private static readonly ResultadoCredito Permitido = new(true, null, Posicion, 50m, 0m);
        private static readonly ResultadoCredito Rechazado = new(false, "LIMITE", Posicion, 400m, 300m);

        public FacturaDomainCreditoTests()
        {
            _domain = new FacturaDomain(_repoFactura.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object,
                _documentoBase.Object, _saldos.Object, _repoSocio.Object, _resolutor.Object, _credito.Object, _contexto, _repoAutorizacion.Object);

            _serie = new NumeracionDocumentoDet { CodigoObj = "6", Serie = 4, SigNumero = 5, Bloqueado = "N", Manual = "N", SubTipoDoc = "--", TipoSerie = "N", NombreSerie = "P" };
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(_serie);
            _resolutor.Setup(r => r.ResolverAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync((string? _, string? _, DateTime? f) => ("CON", (f ?? DateTime.Today).Date));
            _repoFactura.Setup(r => r.InsertarAsync(It.IsAny<Factura>())).ReturnsAsync((Factura c) => { c.Entry = 99; return c; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<FacturaDetalle>())).Returns(Task.CompletedTask);
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>())).Returns(Task.CompletedTask);
            _repoAutorizacion.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>()))
                .Callback<AutorizacionCredito>(a => _autorizaciones.Add(a)).Returns(Task.CompletedTask);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());

            _credito.Setup(c => c.CalcularMontoAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .ReturnsAsync(50m);
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>())).ReturnsAsync(Permitido);
        }

        private static FacturaDetalle Linea(string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            new() { CodArticulo = art, CodAlmacen = "01", Cantidad = cant, Precio = 10m, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea };

        private void Rechaza() =>
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>())).ReturnsAsync(Rechazado);

        private void Autoriza(string? usuario = "admin")
        {
            _contexto.Autorizada = true;
            _contexto.Usuario = usuario;
        }

        /// <summary>Emula un cliente con límite 0 (solo contado): el monto es TotalDoc - PagoInmediato y cualquier monto positivo se rechaza.</summary>
        private void ClienteSoloContado()
        {
            _credito.Setup(c => c.CalcularMontoAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .ReturnsAsync((int _, decimal total, decimal pago, IReadOnlyList<LineaBaseRequest> _) => Math.Max(0m, total - pago));
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>()))
                .ReturnsAsync((string? _, decimal monto) => monto == 0m ? Permitido : Rechazado);
        }

        [Fact]
        public async Task Permitido_CreaYNoRegistraAutorizacion()
        {
            var entry = await _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1", TotalDoc = 50m }, new[] { Linea("A", 1m) });

            Assert.Equal(99, entry);
            _credito.Verify(c => c.CalcularMontoAsync(6, 50m, 0m, It.Is<IReadOnlyList<LineaBaseRequest>>(l => l.Count == 0)), Times.Once);
            _credito.Verify(c => c.VerificarAsync("CL-1", 50m), Times.Once);
            _repoAutorizacion.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>()), Times.Never);
        }

        [Fact]
        public async Task TotalDocNulo_UsaCeroParaCalcularElMonto()
        {
            await _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1", TotalDoc = null }, new[] { Linea("A", 1m) });

            _credito.Verify(c => c.CalcularMontoAsync(6, 0m, 0m, It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Once);
        }

        [Fact]
        public async Task Rechazado_SinAutorizacion_Lanza_SinConsumirConsecutivoNiInsertar()
        {
            Rechaza();

            var ex = await Assert.ThrowsAsync<CreditoExcedidoException>(() =>
                _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1", TotalDoc = 400m }, new[] { Linea("A", 1m) }));

            Assert.Equal("LIMITE_CREDITO_EXCEDIDO", ex.Codigo);
            Assert.Same(Rechazado, ex.Resultado);
            Assert.Equal(5, _serie.SigNumero);
            _repoFactura.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task Rechazado_ConContextoAutorizado_CreaYRegistraUnaSolaFilaTipo6ConElEntryDeLaFactura()
        {
            Rechaza();
            Autoriza("admin");

            var entry = await _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1", TotalDoc = 400m }, new[] { Linea("A", 1m) });

            Assert.Equal(99, entry);
            var fila = Assert.Single(_autorizaciones);
            Assert.Equal((6, 99, "CL-1", "admin", "LIMITE"), (fila.TipoDocumento, fila.EntryDocumento, fila.CodigoSn, fila.Usuario, fila.Motivo));
            Assert.Equal((1000m, 600m, 300m, 0m, 400m, 300m), (fila.Limite, fila.Saldo, fila.Comprometido, fila.Vencido, fila.Monto, fila.Exceso));
            _repoAutorizacion.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>()), Times.Once);
        }

        [Fact]
        public async Task Rechazado_LaFilaSeAgregaTrasAsignarElEntryYDentroDeLaTransaccion()
        {
            Rechaza();
            Autoriza();
            var orden = new List<string>();
            _repoFactura.Setup(r => r.InsertarAsync(It.IsAny<Factura>())).Callback(() => orden.Add("cabecera")).ReturnsAsync((Factura c) => { c.Entry = 99; return c; });
            _repoAutorizacion.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>())).Callback(() => orden.Add("autorizacion")).Returns(Task.CompletedTask);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(async f => { orden.Add("tx"); return await f(); });

            await _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m) });

            Assert.Equal(new[] { "tx", "cabecera", "autorizacion" }, orden);
        }

        [Fact]
        public async Task ContextoAutorizado_PeroSinExceso_NoRegistra()
        {
            Autoriza();

            await _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m) });

            _repoAutorizacion.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AutorizadaConUsuarioVacio_SeTrataComoNoAutorizada(string? usuario)
        {
            Rechaza();
            Autoriza(usuario);

            await Assert.ThrowsAsync<CreditoExcedidoException>(() =>
                _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m) }));

            _repoFactura.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
            Assert.Empty(_autorizaciones);
        }

        [Fact]
        public async Task AutorizadaConUsuarioMayorA50_LanzaErrorDeNegocio()
        {
            Rechaza();
            Autoriza(new string('u', 51));

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m) }));

            Assert.IsNotType<CreditoExcedidoException>(ex);
            Assert.Equal("El usuario que autoriza no es válido.", ex.Message);
            Assert.Equal(5, _serie.SigNumero);
            _repoFactura.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
        }

        // ---- Pago inmediato ----

        [Fact]
        public async Task PagoCompleto_ConLimiteCero_EsPermitido_ConMontoCero()
        {
            ClienteSoloContado();

            var entry = await _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1", TotalDoc = 500m, PagoInmediato = 500m }, new[] { Linea("A", 1m) });

            Assert.Equal(99, entry);
            _credito.Verify(c => c.CalcularMontoAsync(6, 500m, 500m, It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Once);
            _credito.Verify(c => c.VerificarAsync("CL-1", 0m), Times.Once);
            Assert.Empty(_autorizaciones);
        }

        [Fact]
        public async Task SinPago_ConLimiteCero_EsRechazada()
        {
            ClienteSoloContado();

            await Assert.ThrowsAsync<CreditoExcedidoException>(() =>
                _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1", TotalDoc = 500m, PagoInmediato = 0m }, new[] { Linea("A", 1m) }));

            _credito.Verify(c => c.VerificarAsync("CL-1", 500m), Times.Once);
            Assert.Equal(5, _serie.SigNumero);
            _repoFactura.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
        }

        [Fact]
        public async Task PagoParcial_VerificaConElMontoRestante()
        {
            ClienteSoloContado();
            Autoriza();

            await _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1", TotalDoc = 500m, PagoInmediato = 200m }, new[] { Linea("A", 1m) });

            _credito.Verify(c => c.CalcularMontoAsync(6, 500m, 200m, It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Once);
            _credito.Verify(c => c.VerificarAsync("CL-1", 300m), Times.Once);
            Assert.Single(_autorizaciones);
        }

        [Fact]
        public async Task PagoMayorAlTotal_LanzaErrorDeNegocio_SinVerificarNiConsumirConsecutivo()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1", TotalDoc = 100m, PagoInmediato = 100.01m }, new[] { Linea("A", 1m) }));

            Assert.IsNotType<CreditoExcedidoException>(ex);
            Assert.Equal("El pago inmediato no puede exceder el total del documento.", ex.Message);
            Assert.Equal(5, _serie.SigNumero);
            _credito.Verify(c => c.CalcularMontoAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Never);
            _credito.Verify(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>()), Times.Never);
            _repoFactura.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
        }

        [Fact]
        public async Task PagoIgualAlTotal_NoEsError()
        {
            var entry = await _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1", TotalDoc = 100m, PagoInmediato = 100m }, new[] { Linea("A", 1m) });

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task PagoConTotalNulo_SeCompara()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1", TotalDoc = null, PagoInmediato = 1m }, new[] { Linea("A", 1m) }));

            Assert.Equal("El pago inmediato no puede exceder el total del documento.", ex.Message);
        }

        // ---- Facturas con base ----

        [Theory]
        [InlineData(5)]
        [InlineData(4)]
        public async Task ConLineasBase_PasaLasLineasBaseAlCalculo_YLoLiberadoEvitaElDobleConteo(int baseTipo)
        {
            // El servicio devuelve monto 0 (todo lo facturado ya contaba como comprometido en la entrega o el pedido):
            // el cliente al límite puede facturarlo.
            _credito.Setup(c => c.CalcularMontoAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .ReturnsAsync(0m);

            var entry = await _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1", TotalDoc = 264m },
                new[] { Linea("A", 4m, baseTipo: baseTipo, baseEntry: 12, baseLinea: 1) });

            Assert.Equal(99, entry);
            _credito.Verify(c => c.CalcularMontoAsync(6, 264m, 0m,
                It.Is<IReadOnlyList<LineaBaseRequest>>(l => l.Count == 1 && l[0] == new LineaBaseRequest(baseTipo, 12, 1, "A", 4m))), Times.Once);
            _credito.Verify(c => c.VerificarAsync("CL-1", 0m), Times.Once);
        }

        [Fact]
        public async Task LaValidacionDeBaseVaAntesQueElCalculo_YLaVerificacionAntesDeNumerar()
        {
            var orden = new List<string>();
            _documentoBase.Setup(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .Callback(() => orden.Add("validar")).Returns(Task.CompletedTask);
            _resolutor.Setup(r => r.ResolverAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>()))
                .Callback(() => orden.Add("vencimiento")).ReturnsAsync(("CON", DateTime.Today));
            _credito.Setup(c => c.CalcularMontoAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .Callback(() => orden.Add("monto")).ReturnsAsync(50m);
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>()))
                .Callback(() => orden.Add("verificar")).ReturnsAsync(Permitido);
            _repoFactura.Setup(r => r.InsertarAsync(It.IsAny<Factura>())).Callback(() => orden.Add("insertar")).ReturnsAsync((Factura c) => { c.Entry = 99; return c; });

            await _domain.InsertarAsync(new Factura { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m, baseTipo: 5, baseEntry: 12, baseLinea: 1) });

            Assert.Equal(new[] { "validar", "vencimiento", "monto", "verificar", "insertar" }, orden);
            Assert.Equal(6, _serie.SigNumero);
        }

        // ---- Notas de débito ----

        [Fact]
        public void NotaDebitoDomain_NoDependeDelServicioDeCredito_AsiNuncaVerifica()
        {
            var parametros = typeof(NotaDebitoDomain).GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);

            Assert.DoesNotContain(typeof(ICreditoClienteService), parametros);
            Assert.DoesNotContain(typeof(IAutorizacionCreditoContexto), parametros);
        }
    }
}
