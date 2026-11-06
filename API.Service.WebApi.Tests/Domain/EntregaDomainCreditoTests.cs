using API.Domain.Core;
using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>Control del límite de crédito en EntregaDomain.InsertarAsync.</summary>
    public class EntregaDomainCreditoTests
    {
        private readonly Mock<IRepositorioGenerico<Entrega, int>> _repoEntrega = new();
        private readonly Mock<IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<AutorizacionCredito, int>> _repoAutorizacion = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly Mock<ICreditoClienteService> _credito = new();
        private readonly AutorizacionCreditoContexto _contexto = new();
        private readonly NumeracionDocumentoDet _serie;
        private readonly List<AutorizacionCredito> _autorizaciones = new();
        private readonly EntregaDomain _domain;

        private static readonly PosicionCredito Posicion = new("CL-1", 1000m, false, 600m, 0m, 300m, 900m, 100m);
        private static readonly ResultadoCredito Permitido = new(true, null, Posicion, 50m, 0m);
        private static readonly ResultadoCredito Rechazado = new(false, "LIMITE", Posicion, 400m, 300m);

        public EntregaDomainCreditoTests()
        {
            _domain = new EntregaDomain(_repoEntrega.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object,
                _documentoBase.Object, _credito.Object, _contexto, _repoAutorizacion.Object);

            _serie = new NumeracionDocumentoDet { CodigoObj = "5", Serie = 4, SigNumero = 5, Bloqueado = "N", Manual = "N", SubTipoDoc = "--", TipoSerie = "N", NombreSerie = "P" };
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(_serie);
            _repoEntrega.Setup(r => r.InsertarAsync(It.IsAny<Entrega>())).ReturnsAsync((Entrega c) => { c.Entry = 99; return c; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<EntregaDetalle>())).Returns(Task.CompletedTask);
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>())).Returns(Task.CompletedTask);
            _repoAutorizacion.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>()))
                .Callback<AutorizacionCredito>(a => _autorizaciones.Add(a)).Returns(Task.CompletedTask);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());

            _credito.Setup(c => c.CalcularMontoAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .ReturnsAsync(50m);
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>())).ReturnsAsync(Permitido);
        }

        private static EntregaDetalle Linea(string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            new() { CodArticulo = art, CodAlmacen = "01", Cantidad = cant, Precio = 10m, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea };

        private void Rechaza() =>
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>())).ReturnsAsync(Rechazado);

        private void Autoriza(string? usuario = "admin")
        {
            _contexto.Autorizada = true;
            _contexto.Usuario = usuario;
        }

        [Fact]
        public async Task Permitido_CreaYNoRegistraAutorizacion()
        {
            var entry = await _domain.InsertarAsync(new Entrega { Serie = 4, CodigoSn = "CL-1", TotalDoc = 50m }, new[] { Linea("A", 1m) });

            Assert.Equal(99, entry);
            _credito.Verify(c => c.CalcularMontoAsync(5, 50m, 0m, It.Is<IReadOnlyList<LineaBaseRequest>>(l => l.Count == 0)), Times.Once);
            _credito.Verify(c => c.VerificarAsync("CL-1", 50m), Times.Once);
            _repoAutorizacion.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>()), Times.Never);
        }

        [Fact]
        public async Task TotalDocNulo_UsaCeroParaCalcularElMonto()
        {
            await _domain.InsertarAsync(new Entrega { Serie = 4, CodigoSn = "CL-1", TotalDoc = null }, new[] { Linea("A", 1m) });

            _credito.Verify(c => c.CalcularMontoAsync(5, 0m, 0m, It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Once);
        }

        [Fact]
        public async Task Rechazado_SinAutorizacion_Lanza_SinConsumirConsecutivoNiInsertar()
        {
            Rechaza();

            var ex = await Assert.ThrowsAsync<CreditoExcedidoException>(() =>
                _domain.InsertarAsync(new Entrega { Serie = 4, CodigoSn = "CL-1", TotalDoc = 400m }, new[] { Linea("A", 1m) }));

            Assert.Equal("LIMITE_CREDITO_EXCEDIDO", ex.Codigo);
            Assert.Same(Rechazado, ex.Resultado);
            Assert.Equal(5, _serie.SigNumero);
            _repoEntrega.Verify(r => r.InsertarAsync(It.IsAny<Entrega>()), Times.Never);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task Rechazado_ConContextoAutorizado_CreaYRegistraUnaFilaTipo5()
        {
            Rechaza();
            Autoriza("admin");

            var entry = await _domain.InsertarAsync(new Entrega { Serie = 4, CodigoSn = "CL-1", TotalDoc = 400m }, new[] { Linea("A", 1m) });

            Assert.Equal(99, entry);
            var fila = Assert.Single(_autorizaciones);
            Assert.Equal((5, 99, "CL-1", "admin", "LIMITE"), (fila.TipoDocumento, fila.EntryDocumento, fila.CodigoSn, fila.Usuario, fila.Motivo));
            Assert.Equal((1000m, 600m, 300m, 0m, 400m, 300m), (fila.Limite, fila.Saldo, fila.Comprometido, fila.Vencido, fila.Monto, fila.Exceso));
        }

        [Fact]
        public async Task Rechazado_LaFilaSeAgregaTrasAsignarElEntryYDentroDeLaTransaccion()
        {
            Rechaza();
            Autoriza();
            var orden = new List<string>();
            _repoEntrega.Setup(r => r.InsertarAsync(It.IsAny<Entrega>())).Callback(() => orden.Add("cabecera")).ReturnsAsync((Entrega c) => { c.Entry = 99; return c; });
            _repoAutorizacion.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>())).Callback(() => orden.Add("autorizacion")).Returns(Task.CompletedTask);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(async f => { orden.Add("tx"); return await f(); });

            await _domain.InsertarAsync(new Entrega { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m) });

            Assert.Equal(new[] { "tx", "cabecera", "autorizacion" }, orden);
        }

        [Fact]
        public async Task ContextoAutorizado_PeroSinExceso_NoRegistra()
        {
            Autoriza();

            await _domain.InsertarAsync(new Entrega { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m) });

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
                _domain.InsertarAsync(new Entrega { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m) }));

            _repoEntrega.Verify(r => r.InsertarAsync(It.IsAny<Entrega>()), Times.Never);
            Assert.Empty(_autorizaciones);
        }

        [Fact]
        public async Task AutorizadaConUsuarioMayorA50_LanzaErrorDeNegocio()
        {
            Rechaza();
            Autoriza(new string('u', 51));

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new Entrega { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m) }));

            Assert.IsNotType<CreditoExcedidoException>(ex);
            Assert.Equal("El usuario que autoriza no es válido.", ex.Message);
            Assert.Equal(5, _serie.SigNumero);
            _repoEntrega.Verify(r => r.InsertarAsync(It.IsAny<Entrega>()), Times.Never);
        }

        [Fact]
        public async Task CopiadaDePedido_UsaLasLineasBaseParaCalcularElMonto()
        {
            await _domain.InsertarAsync(new Entrega { Serie = 4, CodigoSn = "CL-1", TotalDoc = 80m },
                new[] { Linea("A", 4m, baseTipo: 4, baseEntry: 12, baseLinea: 1), Linea("B", 1m) });

            _credito.Verify(c => c.CalcularMontoAsync(5, 80m, 0m,
                It.Is<IReadOnlyList<LineaBaseRequest>>(l => l.Count == 1 && l[0] == new LineaBaseRequest(4, 12, 1, "A", 4m))), Times.Once);
        }

        [Fact]
        public async Task LaValidacionDeBaseVaAntesQueElCalculoDelMonto_YLaVerificacionAntesDeNumerar()
        {
            var orden = new List<string>();
            _documentoBase.Setup(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .Callback(() => orden.Add("validar")).Returns(Task.CompletedTask);
            _credito.Setup(c => c.CalcularMontoAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .Callback(() => orden.Add("monto")).ReturnsAsync(50m);
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>()))
                .Callback(() => orden.Add("verificar")).ReturnsAsync(Permitido);
            _repoEntrega.Setup(r => r.InsertarAsync(It.IsAny<Entrega>())).Callback(() => orden.Add("insertar")).ReturnsAsync((Entrega c) => { c.Entry = 99; return c; });

            await _domain.InsertarAsync(new Entrega { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m, baseTipo: 4, baseEntry: 12, baseLinea: 1) });

            Assert.Equal(new[] { "validar", "monto", "verificar", "insertar" }, orden);
            Assert.Equal(6, _serie.SigNumero);
        }
    }
}
