using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>Control del límite de crédito en PedidoDomain: alta con líneas, reapertura y cambio de socio.</summary>
    public class PedidoDomainCreditoTests
    {
        private readonly Mock<IRepositorioGenerico<Pedido, int>> _repoPedido = new();
        private readonly Mock<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<AutorizacionCredito, int>> _repoAutorizacion = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly Mock<ICreditoClienteService> _credito = new();
        private readonly AutorizacionCreditoContexto _contexto = new();
        private readonly NumeracionDocumentoDet _serie;
        private readonly List<AutorizacionCredito> _autorizaciones = new();
        private readonly PedidoDomain _domain;

        private static readonly PosicionCredito Posicion = new("CL-1", 1000m, false, 600m, 0m, 300m, 900m, 100m);
        private static readonly ResultadoCredito Permitido = new(true, null, Posicion, 50m, 0m);
        private static readonly ResultadoCredito Rechazado = new(false, "LIMITE", Posicion, 400m, 300m);

        public PedidoDomainCreditoTests()
        {
            _domain = new PedidoDomain(_repoPedido.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object,
                _documentoBase.Object, _credito.Object, _contexto, _repoAutorizacion.Object);

            _serie = new NumeracionDocumentoDet { CodigoObj = "4", Serie = 4, SigNumero = 5, Bloqueado = "N", Manual = "N", SubTipoDoc = "--", TipoSerie = "N", NombreSerie = "P" };
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(_serie);
            _repoPedido.Setup(r => r.InsertarAsync(It.IsAny<Pedido>())).ReturnsAsync((Pedido c) => { c.Entry = 99; return c; });
            _repoPedido.Setup(r => r.ActualizarAsync(1, It.IsAny<Pedido>())).ReturnsAsync(true);
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<PedidoDetalle>())).Returns(Task.CompletedTask);
            _repoAutorizacion.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>()))
                .Callback<AutorizacionCredito>(a => _autorizaciones.Add(a)).Returns(Task.CompletedTask);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());

            _credito.Setup(c => c.CalcularMontoAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .ReturnsAsync(50m);
            _credito.Setup(c => c.ValorAbiertoDocumentoAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(50m);
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>())).ReturnsAsync(Permitido);
        }

        private static PedidoDetalle Linea(string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            new() { CodArticulo = art, Cantidad = cant, Precio = 10m, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea };

        private void Rechaza() =>
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>())).ReturnsAsync(Rechazado);

        private void Autoriza(string? usuario = "admin")
        {
            _contexto.Autorizada = true;
            _contexto.Usuario = usuario;
        }

        private void ConPedidoGuardado(string estadoDoc, string cancelado, string codigoSn, params PedidoDetalle[] lineas)
        {
            _repoPedido.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Pedido { Entry = 1, EstadoDoc = estadoDoc, Cancelado = cancelado, CodigoSn = codigoSn });
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.Select(l => { l.Entry = 1; return l; }).ToList().AsAsyncQueryable());
        }

        // ---- Alta con líneas ----

        [Fact]
        public async Task Alta_Permitido_CreaYNoRegistraAutorizacion()
        {
            var obj = new Pedido { Serie = 4, CodigoSn = "CL-1", TotalDoc = 50m };

            var entry = await _domain.InsertarAsync(obj, new[] { Linea("A", 1m) });

            Assert.Equal(99, entry);
            _credito.Verify(c => c.CalcularMontoAsync(4, 50m, 0m, It.Is<IReadOnlyList<LineaBaseRequest>>(l => l.Count == 0)), Times.Once);
            _credito.Verify(c => c.VerificarAsync("CL-1", 50m), Times.Once);
            _repoAutorizacion.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>()), Times.Never);
        }

        [Fact]
        public async Task Alta_TotalDocNulo_UsaCeroParaCalcularElMonto()
        {
            await _domain.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1", TotalDoc = null }, new[] { Linea("A", 1m) });

            _credito.Verify(c => c.CalcularMontoAsync(4, 0m, 0m, It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Once);
        }

        [Fact]
        public async Task Alta_Rechazado_SinAutorizacion_Lanza_SinConsumirConsecutivoNiInsertar()
        {
            Rechaza();

            var ex = await Assert.ThrowsAsync<CreditoExcedidoException>(() =>
                _domain.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1", TotalDoc = 400m }, new[] { Linea("A", 1m) }));

            Assert.Equal("LIMITE_CREDITO_EXCEDIDO", ex.Codigo);
            Assert.Same(Rechazado, ex.Resultado);
            Assert.Equal(5, _serie.SigNumero);
            _repoPedido.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
        }

        [Fact]
        public async Task Alta_Rechazado_ConContextoAutorizado_CreaYRegistraUnaFila()
        {
            Rechaza();
            Autoriza("admin");

            var entry = await _domain.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1", TotalDoc = 400m }, new[] { Linea("A", 1m) });

            Assert.Equal(99, entry);
            var fila = Assert.Single(_autorizaciones);
            Assert.Equal((4, 99, "CL-1", "admin", "LIMITE"), (fila.TipoDocumento, fila.EntryDocumento, fila.CodigoSn, fila.Usuario, fila.Motivo));
            Assert.Equal((1000m, 600m, 300m, 0m, 400m, 300m), (fila.Limite, fila.Saldo, fila.Comprometido, fila.Vencido, fila.Monto, fila.Exceso));
        }

        [Fact]
        public async Task Alta_Rechazado_LaFilaSeAgregaTrasAsignarElEntryYDentroDeLaTransaccion()
        {
            Rechaza();
            Autoriza();
            var orden = new List<string>();
            _repoPedido.Setup(r => r.InsertarAsync(It.IsAny<Pedido>())).Callback(() => orden.Add("cabecera")).ReturnsAsync((Pedido c) => { c.Entry = 99; return c; });
            _repoAutorizacion.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>())).Callback(() => orden.Add("autorizacion")).Returns(Task.CompletedTask);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(async f => { orden.Add("tx"); return await f(); });

            await _domain.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m) });

            Assert.Equal(new[] { "tx", "cabecera", "autorizacion" }, orden);
        }

        [Fact]
        public async Task Alta_ContextoAutorizado_PeroSinExceso_NoRegistra()
        {
            Autoriza();

            await _domain.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m) });

            _repoAutorizacion.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Alta_AutorizadaConUsuarioVacio_SeTrataComoNoAutorizada(string? usuario)
        {
            Rechaza();
            Autoriza(usuario);

            await Assert.ThrowsAsync<CreditoExcedidoException>(() =>
                _domain.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m) }));

            _repoPedido.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
            Assert.Empty(_autorizaciones);
        }

        [Fact]
        public async Task Alta_AutorizadaConUsuarioMayorA50_LanzaErrorDeNegocio()
        {
            Rechaza();
            Autoriza(new string('u', 51));

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m) }));

            Assert.IsNotType<CreditoExcedidoException>(ex);
            Assert.Equal("El usuario que autoriza no es válido.", ex.Message);
            Assert.Equal(5, _serie.SigNumero);
            _repoPedido.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task Alta_SinLineas_NoVerifica()
        {
            Rechaza();

            var entry = await _domain.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1", TotalDoc = 400m });

            Assert.Equal(99, entry);
            _credito.Verify(c => c.CalcularMontoAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Never);
            _credito.Verify(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>()), Times.Never);
        }

        [Fact]
        public async Task Alta_CopiadaDeCotizacion_UsaLasLineasBaseParaCalcularElMonto()
        {
            await _domain.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1", TotalDoc = 80m },
                new[] { Linea("A", 4m, baseTipo: 3, baseEntry: 12, baseLinea: 1), Linea("B", 1m) });

            _credito.Verify(c => c.CalcularMontoAsync(4, 80m, 0m,
                It.Is<IReadOnlyList<LineaBaseRequest>>(l => l.Count == 1 && l[0] == new LineaBaseRequest(3, 12, 1, "A", 4m))), Times.Once);
        }

        [Fact]
        public async Task Alta_LaValidacionDeBaseVaAntesQueElCalculoDelMonto()
        {
            var orden = new List<string>();
            _documentoBase.Setup(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .Callback(() => orden.Add("validar")).Returns(Task.CompletedTask);
            _credito.Setup(c => c.CalcularMontoAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .Callback(() => orden.Add("monto")).ReturnsAsync(50m);
            _credito.Setup(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>()))
                .Callback(() => orden.Add("verificar")).ReturnsAsync(Permitido);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).Callback(() => orden.Add("numerar")).ReturnsAsync(_serie);

            await _domain.InsertarAsync(new Pedido { Serie = 4, CodigoSn = "CL-1" }, new[] { Linea("A", 1m, baseTipo: 3, baseEntry: 12, baseLinea: 1) });

            Assert.Equal(new[] { "validar", "monto", "verificar", "numerar" }, orden);
        }

        // ---- Reapertura y cambio de socio ----

        [Theory]
        [InlineData("C", "N")]
        [InlineData("A", "S")]
        public async Task Reabrir_VerificaConElValorAbiertoDelPedido(string estado, string cancelado)
        {
            ConPedidoGuardado(estado, cancelado, "CL-1", Linea("A", 1m));

            Assert.True(await _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "A", Cancelado = "N", CodigoSn = "CL-1" }));

            _credito.Verify(c => c.ValorAbiertoDocumentoAsync(4, 1), Times.Once);
            _credito.Verify(c => c.VerificarAsync("CL-1", 50m), Times.Once);
            _repoAutorizacion.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>()), Times.Never);
        }

        [Fact]
        public async Task Reabrir_Rechazado_Lanza_YElEstadoNoCambia()
        {
            ConPedidoGuardado("C", "N", "CL-1", Linea("A", 1m));
            Rechaza();

            await Assert.ThrowsAsync<CreditoExcedidoException>(() => _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-1" }));

            _repoPedido.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<Pedido>()), Times.Never);
            _repoAutorizacion.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>()), Times.Never);
        }

        [Fact]
        public async Task Reabrir_Rechazado_ConAutorizacion_ReabreYRegistraEnLaMismaTransaccion()
        {
            ConPedidoGuardado("C", "N", "CL-1", Linea("A", 1m));
            Rechaza();
            Autoriza("admin");
            var orden = new List<string>();
            _repoPedido.Setup(r => r.ActualizarAsync(1, It.IsAny<Pedido>())).Callback(() => orden.Add("actualizar")).ReturnsAsync(true);
            _repoAutorizacion.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>())).Callback<AutorizacionCredito>(a => { orden.Add("autorizacion"); _autorizaciones.Add(a); }).Returns(Task.CompletedTask);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(async f => { orden.Add("tx"); return await f(); });

            Assert.True(await _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-1" }));

            Assert.Equal(new[] { "tx", "actualizar", "autorizacion" }, orden);
            var fila = Assert.Single(_autorizaciones);
            Assert.Equal((4, 1, "CL-1", "admin", "LIMITE", 400m, 300m), (fila.TipoDocumento, fila.EntryDocumento, fila.CodigoSn, fila.Usuario, fila.Motivo, fila.Monto, fila.Exceso));
        }

        [Fact]
        public async Task Reabrir_AutorizadaConUsuarioVacio_Lanza()
        {
            ConPedidoGuardado("C", "N", "CL-1", Linea("A", 1m));
            Rechaza();
            Autoriza("  ");

            await Assert.ThrowsAsync<CreditoExcedidoException>(() => _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-1" }));

            _repoPedido.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task Reabrir_ContextoAutorizadoPeroPermitido_ActualizaSinRegistrar()
        {
            ConPedidoGuardado("C", "N", "CL-1", Linea("A", 1m));
            Autoriza();

            Assert.True(await _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-1" }));

            _repoAutorizacion.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<AutorizacionCredito>()), Times.Never);
            _repoPedido.Verify(r => r.ActualizarAsync(1, It.IsAny<Pedido>()), Times.Once);
        }

        [Fact]
        public async Task Reabrir_PedidoSinLineas_NoVerifica()
        {
            ConPedidoGuardado("C", "N", "CL-1");
            Rechaza();

            Assert.True(await _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-1" }));

            _credito.Verify(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>()), Times.Never);
        }

        [Fact]
        public async Task CambiarSocio_SinLineasCopiadas_VerificaAlNuevoSocio()
        {
            ConPedidoGuardado("A", "N", "CL-1", Linea("A", 1m));

            Assert.True(await _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-2" }));

            _credito.Verify(c => c.VerificarAsync("CL-2", 50m), Times.Once);
        }

        [Fact]
        public async Task CambiarSocio_Rechazado_Lanza_YNoGuarda()
        {
            ConPedidoGuardado("A", "N", "CL-1", Linea("A", 1m));
            Rechaza();

            await Assert.ThrowsAsync<CreditoExcedidoException>(() => _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-2" }));

            _repoPedido.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task CambiarSocio_ConAutorizacion_RegistraConElNuevoSocio()
        {
            ConPedidoGuardado("A", "N", "CL-1", Linea("A", 1m));
            Rechaza();
            Autoriza();

            Assert.True(await _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-2" }));

            var fila = Assert.Single(_autorizaciones);
            Assert.Equal(("CL-2", 1), (fila.CodigoSn, fila.EntryDocumento));
        }

        [Fact]
        public async Task Cancelar_NoVerifica()
        {
            ConPedidoGuardado("A", "N", "CL-1", Linea("A", 1m));
            Rechaza();

            Assert.True(await _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "C", CodigoSn = "CL-1" }));

            _credito.Verify(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>()), Times.Never);
            _credito.Verify(c => c.ValorAbiertoDocumentoAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarSoloElComentario_NoVerifica()
        {
            ConPedidoGuardado("A", "N", "CL-1", Linea("A", 1m));
            Rechaza();

            Assert.True(await _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-1", Comentario = "nota" }));

            _credito.Verify(c => c.VerificarAsync(It.IsAny<string?>(), It.IsAny<decimal>()), Times.Never);
        }
    }
}
