using API.Application.DTO;
using API.Application.DTO.creditoCliente;
using API.Application.DTO.entrega;
using API.Application.DTO.factura;
using API.Application.DTO.pedido;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    /// <summary>
    /// Posición de crédito del cliente (consulta) y traducción de CreditoExcedidoException a RespuestaCredito
    /// en las aplicaciones de Pedido, Entrega y Factura.
    /// </summary>
    public class CreditoClienteApplicationTests
    {
        private readonly Mock<ICreditoClienteService> _credito = new();
        private readonly CreditoClienteApplication _app;

        public CreditoClienteApplicationTests()
        {
            _app = new CreditoClienteApplication(_credito.Object);
        }

        private static PosicionCredito Posicion(string codigo = "C001", decimal? limite = 5000m, bool bloquear = false,
            decimal saldo = 2000m, decimal vencido = 0m, decimal comprometido = 1000m, decimal exposicion = 3000m, decimal? disponible = 2000m)
            => new(codigo, limite, bloquear, saldo, vencido, comprometido, exposicion, disponible);

        // ---------- ObtenerPosicionAsync ----------

        [Fact]
        public async Task ObtenerPosicion_MapeaLosCamposYCalculaElPorcentajeDeUso()
        {
            _credito.Setup(c => c.ObtenerPosicionAsync("C001"))
                .ReturnsAsync(Posicion(limite: 5000m, bloquear: true, saldo: 2000m, vencido: 150m, comprometido: 1000m, exposicion: 3000m, disponible: 2000m));

            var r = await _app.ObtenerPosicionAsync("C001");

            Assert.True(r.Resultado);
            var dto = r.Dato;
            Assert.Equal("C001", dto.CodigoSn);
            Assert.Equal(5000m, dto.Limite);
            Assert.True(dto.BloquearSiVencido);
            Assert.Equal(2000m, dto.Saldo);
            Assert.Equal(150m, dto.Vencido);
            Assert.Equal(1000m, dto.Comprometido);
            Assert.Equal(3000m, dto.Exposicion);
            Assert.Equal(2000m, dto.Disponible);
            Assert.Equal(60.0m, dto.PorcentajeUso);
        }

        [Fact]
        public async Task ObtenerPosicion_PorcentajeRedondeadoAUnDecimal()
        {
            _credito.Setup(c => c.ObtenerPosicionAsync("C001"))
                .ReturnsAsync(Posicion(limite: 10000m, exposicion: 3333m, disponible: 6667m));

            var r = await _app.ObtenerPosicionAsync("C001");

            Assert.Equal(33.3m, r.Dato.PorcentajeUso);
        }

        [Fact]
        public async Task ObtenerPosicion_ExposicionSobreElLimite_PorcentajeMayorACien()
        {
            _credito.Setup(c => c.ObtenerPosicionAsync("C001"))
                .ReturnsAsync(Posicion(limite: 1000m, exposicion: 1250m, disponible: -250m));

            var r = await _app.ObtenerPosicionAsync("C001");

            Assert.Equal(125.0m, r.Dato.PorcentajeUso);
            Assert.Equal(-250m, r.Dato.Disponible);
        }

        [Fact]
        public async Task ObtenerPosicion_LimiteCero_PorcentajeNulo()
        {
            _credito.Setup(c => c.ObtenerPosicionAsync("C001"))
                .ReturnsAsync(Posicion(limite: 0m, exposicion: 300m, disponible: -300m));

            var r = await _app.ObtenerPosicionAsync("C001");

            Assert.True(r.Resultado);
            Assert.Null(r.Dato.PorcentajeUso);
            Assert.Equal(0m, r.Dato.Limite);
        }

        [Fact]
        public async Task ObtenerPosicion_SinLimite_PorcentajeYDisponibleNulos()
        {
            _credito.Setup(c => c.ObtenerPosicionAsync("C001"))
                .ReturnsAsync(Posicion(limite: null, disponible: null));

            var r = await _app.ObtenerPosicionAsync("C001");

            Assert.Null(r.Dato.Limite);
            Assert.Null(r.Dato.Disponible);
            Assert.Null(r.Dato.PorcentajeUso);
        }

        [Fact]
        public async Task ObtenerPosicion_ClienteInexistente_ResultadoVerdaderoConDatoNulo()
        {
            _credito.Setup(c => c.ObtenerPosicionAsync("X")).ReturnsAsync((PosicionCredito?)null);

            var r = await _app.ObtenerPosicionAsync("X");

            Assert.True(r.Resultado);
            Assert.Null(r.Dato);
            Assert.Equal("El cliente no existe.", r.Mensaje);
        }

        [Fact]
        public async Task ObtenerPosicion_ErrorDelServicio_DevuelveElMensaje()
        {
            _credito.Setup(c => c.ObtenerPosicionAsync("C001")).ThrowsAsync(new Exception("falla"));

            var r = await _app.ObtenerPosicionAsync("C001");

            Assert.False(r.Resultado);
            Assert.Equal("falla", r.Mensaje);
        }

        // ---------- ObtenerPosicionesAsync ----------

        [Fact]
        public async Task ObtenerPosiciones_DepuraOrdenaYConsultaUnaVez()
        {
            IEnumerable<string>? consultados = null;
            _credito.Setup(c => c.ObtenerPosicionesAsync(It.IsAny<IEnumerable<string>>()))
                .Callback<IEnumerable<string>>(cs => consultados = cs.ToList())
                .ReturnsAsync(new Dictionary<string, PosicionCredito>
                {
                    ["C010"] = Posicion("C010"),
                    ["C002"] = Posicion("C002", limite: 0m),
                    ["C001"] = Posicion("C001")
                });

            var r = await _app.ObtenerPosicionesAsync(new[] { " C010 ", "C002", null!, "", "   ", "C002", "C001" });

            Assert.True(r.Resultado);
            Assert.Equal(new[] { "C001", "C002", "C010" }, r.Dato.Select(p => p.CodigoSn));
            Assert.Equal(new[] { "C001", "C002", "C010" }, consultados!.OrderBy(c => c, StringComparer.Ordinal));
            Assert.Null(r.Dato.Single(p => p.CodigoSn == "C002").PorcentajeUso);
            Assert.Equal(60.0m, r.Dato.Single(p => p.CodigoSn == "C001").PorcentajeUso);
        }

        [Fact]
        public async Task ObtenerPosiciones_NuloOVacio_ListaVaciaSinConsultar()
        {
            var nulo = await _app.ObtenerPosicionesAsync(null);
            var vacio = await _app.ObtenerPosicionesAsync(new[] { "", "  " });

            Assert.True(nulo.Resultado);
            Assert.Empty(nulo.Dato);
            Assert.True(vacio.Resultado);
            Assert.Empty(vacio.Dato);
            _credito.Verify(c => c.ObtenerPosicionesAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerPosiciones_Exactamente500_Pasa_501_SeRechaza()
        {
            _credito.Setup(c => c.ObtenerPosicionesAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(new Dictionary<string, PosicionCredito>());

            var justo = await _app.ObtenerPosicionesAsync(Enumerable.Range(1, 500).Select(i => $"C{i:000}"));
            var excedido = await _app.ObtenerPosicionesAsync(Enumerable.Range(1, 501).Select(i => $"C{i:000}"));

            Assert.True(justo.Resultado);
            Assert.False(excedido.Resultado);
            Assert.Equal("Demasiados socios en la consulta.", excedido.Mensaje);
            _credito.Verify(c => c.ObtenerPosicionesAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
        }

        [Fact]
        public async Task ObtenerPosiciones_ErrorDelServicio_DevuelveElMensaje()
        {
            _credito.Setup(c => c.ObtenerPosicionesAsync(It.IsAny<IEnumerable<string>>())).ThrowsAsync(new Exception("falla"));

            var r = await _app.ObtenerPosicionesAsync(new[] { "C001" });

            Assert.False(r.Resultado);
            Assert.Equal("falla", r.Mensaje);
        }

        // ---------- CreditoExcedidoException -> RespuestaCredito en Pedido, Entrega y Factura ----------

        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();

        private static CreditoExcedidoException ExcesoLimite() =>
            CreditoExcedidoException.Para(new ResultadoCredito(false, "LIMITE",
                new PosicionCredito("C001", 5000m, false, 3000m, 0m, 1500m, 4500m, 500m), 800m, 300m), "C001");

        private static CreditoExcedidoException ExcesoVencido() =>
            CreditoExcedidoException.Para(new ResultadoCredito(false, "VENCIDO",
                new PosicionCredito("C001", 5000m, true, 3000m, 700m, 0m, 3000m, 2000m), 800m, 800m), "C001");

        private static void AssertCreditoLimite(CreditoExcedidoDTO? credito)
        {
            Assert.NotNull(credito);
            Assert.Equal("LIMITE_CREDITO_EXCEDIDO", credito!.Codigo);
            Assert.Equal(5000m, credito.Limite);
            Assert.Equal(3000m, credito.Saldo);
            Assert.Equal(1500m, credito.Comprometido);
            Assert.Equal(0m, credito.Vencido);
            Assert.Equal(800m, credito.Monto);
            Assert.Equal(300m, credito.Exceso);
            Assert.Equal(500m, credito.Disponible);
        }

        [Fact]
        public async Task Pedido_Insertar_ConLineas_ExcesoDeLimite_DevuelveRespuestaCredito()
        {
            var ex = ExcesoLimite();
            var domain = new Mock<IPedidoDomain>();
            domain.Setup(d => d.InsertarAsync(It.IsAny<Pedido>(), It.IsAny<IEnumerable<PedidoDetalle>>())).ThrowsAsync(ex);
            var app = new PedidoApplication(domain.Object, _mapper);

            var r = await app.InsertarAsync(new PedidoCrearDTO { Serie = 7, CodigoSn = "C001", Lineas = { new PedidoDetalleCrearDTO { CodArticulo = "A", Cantidad = 1m } } });

            var rc = Assert.IsType<RespuestaCredito<int>>(r);
            Assert.False(rc.Resultado);
            Assert.Equal(ex.Message, rc.Mensaje);
            Assert.Equal(0, rc.Dato);
            AssertCreditoLimite(rc.Credito);
        }

        [Fact]
        public async Task Pedido_Insertar_SinLineas_ExcesoPorVencido_DevuelveRespuestaCreditoConElCodigoDeVencido()
        {
            var ex = ExcesoVencido();
            var domain = new Mock<IPedidoDomain>();
            domain.Setup(d => d.InsertarAsync(It.IsAny<Pedido>())).ThrowsAsync(ex);
            var app = new PedidoApplication(domain.Object, _mapper);

            var r = await app.InsertarAsync(new PedidoCrearDTO { Serie = 7, CodigoSn = "C001" });

            var rc = Assert.IsType<RespuestaCredito<int>>(r);
            Assert.False(rc.Resultado);
            Assert.Equal(ex.Message, rc.Mensaje);
            Assert.Equal("CLIENTE_CON_VENCIDO", rc.Credito!.Codigo);
            Assert.Equal(700m, rc.Credito.Vencido);
            Assert.Equal(800m, rc.Credito.Monto);
            Assert.Equal(800m, rc.Credito.Exceso);
        }

        [Fact]
        public async Task Pedido_Insertar_OtroError_SigueSiendoUnaRespuestaNormal()
        {
            var domain = new Mock<IPedidoDomain>();
            domain.Setup(d => d.InsertarAsync(It.IsAny<Pedido>(), It.IsAny<IEnumerable<PedidoDetalle>>())).ThrowsAsync(new Exception("otro"));
            var app = new PedidoApplication(domain.Object, _mapper);

            var r = await app.InsertarAsync(new PedidoCrearDTO { Serie = 7, Lineas = { new PedidoDetalleCrearDTO { CodArticulo = "A", Cantidad = 1m } } });

            Assert.False(r.Resultado);
            Assert.Equal("otro", r.Mensaje);
            Assert.IsNotType<RespuestaCredito<int>>(r);
        }

        [Fact]
        public async Task Pedido_Actualizar_ExcesoAlReabrir_DevuelveRespuestaCreditoBool()
        {
            var ex = ExcesoLimite();
            var domain = new Mock<IPedidoDomain>();
            domain.Setup(d => d.ActualizarAsync(5, It.IsAny<Pedido>())).ThrowsAsync(ex);
            var app = new PedidoApplication(domain.Object, _mapper);

            var r = await app.ActualizarAsync(5, new PedidoActualizarDTO { CodigoSn = "C001" });

            var rc = Assert.IsType<RespuestaCredito<bool>>(r);
            Assert.False(rc.Resultado);
            Assert.False(rc.Dato);
            Assert.Equal(ex.Message, rc.Mensaje);
            AssertCreditoLimite(rc.Credito);
        }

        [Fact]
        public async Task Pedido_Actualizar_OtroError_SigueSiendoUnaRespuestaNormal()
        {
            var domain = new Mock<IPedidoDomain>();
            domain.Setup(d => d.ActualizarAsync(5, It.IsAny<Pedido>())).ThrowsAsync(new Exception("otro"));
            var app = new PedidoApplication(domain.Object, _mapper);

            var r = await app.ActualizarAsync(5, new PedidoActualizarDTO());

            Assert.False(r.Resultado);
            Assert.Equal("otro", r.Mensaje);
            Assert.IsNotType<RespuestaCredito<bool>>(r);
        }

        [Fact]
        public async Task Entrega_Insertar_Exceso_DevuelveRespuestaCredito()
        {
            var ex = ExcesoLimite();
            var domain = new Mock<IEntregaDomain>();
            domain.Setup(d => d.InsertarAsync(It.IsAny<Entrega>(), It.IsAny<IEnumerable<EntregaDetalle>>())).ThrowsAsync(ex);
            var app = new EntregaApplication(domain.Object, _mapper);

            var r = await app.InsertarAsync(new EntregaCrearDTO { Serie = 4, CodigoSn = "C001" });

            var rc = Assert.IsType<RespuestaCredito<int>>(r);
            Assert.False(rc.Resultado);
            Assert.Equal(ex.Message, rc.Mensaje);
            AssertCreditoLimite(rc.Credito);
        }

        [Fact]
        public async Task Entrega_Insertar_OtroError_SigueSiendoUnaRespuestaNormal()
        {
            var domain = new Mock<IEntregaDomain>();
            domain.Setup(d => d.InsertarAsync(It.IsAny<Entrega>(), It.IsAny<IEnumerable<EntregaDetalle>>())).ThrowsAsync(new Exception("otro"));
            var app = new EntregaApplication(domain.Object, _mapper);

            var r = await app.InsertarAsync(new EntregaCrearDTO { Serie = 4 });

            Assert.False(r.Resultado);
            Assert.IsNotType<RespuestaCredito<int>>(r);
        }

        [Fact]
        public async Task Factura_Insertar_Exceso_DevuelveRespuestaCredito()
        {
            var ex = ExcesoVencido();
            var domain = new Mock<IFacturaDomain>();
            domain.Setup(d => d.InsertarAsync(It.IsAny<Factura>(), It.IsAny<IEnumerable<FacturaDetalle>>())).ThrowsAsync(ex);
            var app = new FacturaApplication(domain.Object, _mapper);

            var r = await app.InsertarAsync(new FacturaCrearDTO { Serie = 4, CodigoSn = "C001" });

            var rc = Assert.IsType<RespuestaCredito<int>>(r);
            Assert.False(rc.Resultado);
            Assert.Equal("CLIENTE_CON_VENCIDO", rc.Credito!.Codigo);
        }

        [Fact]
        public async Task Factura_Insertar_PagoInmediatoNegativo_Falla_SinLlamarAlDominio()
        {
            var domain = new Mock<IFacturaDomain>();
            var app = new FacturaApplication(domain.Object, _mapper);

            var r = await app.InsertarAsync(new FacturaCrearDTO { Serie = 4, CodigoSn = "C001", PagoInmediato = -0.01m });

            Assert.False(r.Resultado);
            Assert.Equal("El pago inmediato no puede ser negativo.", r.Mensaje);
            Assert.IsNotType<RespuestaCredito<int>>(r);
            domain.Verify(d => d.InsertarAsync(It.IsAny<Factura>(), It.IsAny<IEnumerable<FacturaDetalle>>()), Times.Never);
        }

        [Theory]
        [InlineData(null, 0.0)]
        [InlineData(0.0, 0.0)]
        [InlineData(250.5, 250.5)]
        public async Task Factura_Insertar_PagoInmediatoNoNegativo_LlegaAlDominio(double? pago, double esperado)
        {
            Factura? recibida = null;
            var domain = new Mock<IFacturaDomain>();
            domain.Setup(d => d.InsertarAsync(It.IsAny<Factura>(), It.IsAny<IEnumerable<FacturaDetalle>>()))
                .Callback<Factura, IEnumerable<FacturaDetalle>>((f, _) => recibida = f)
                .ReturnsAsync(11);
            var app = new FacturaApplication(domain.Object, _mapper);

            var r = await app.InsertarAsync(new FacturaCrearDTO { Serie = 4, CodigoSn = "C001", PagoInmediato = pago.HasValue ? (decimal)pago.Value : null });

            Assert.True(r.Resultado);
            Assert.Equal(11, r.Dato);
            Assert.Equal((decimal)esperado, recibida!.PagoInmediato);
        }

        [Fact]
        public async Task Factura_Insertar_OtroError_SigueSiendoUnaRespuestaNormal()
        {
            var domain = new Mock<IFacturaDomain>();
            domain.Setup(d => d.InsertarAsync(It.IsAny<Factura>(), It.IsAny<IEnumerable<FacturaDetalle>>())).ThrowsAsync(new Exception("otro"));
            var app = new FacturaApplication(domain.Object, _mapper);

            var r = await app.InsertarAsync(new FacturaCrearDTO { Serie = 4 });

            Assert.False(r.Resultado);
            Assert.Equal("otro", r.Mensaje);
            Assert.IsNotType<RespuestaCredito<int>>(r);
        }
    }
}
