using API.Application.DTO.pagoEfectuado;
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
    public class PagoEfectuadoApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<IPagoEfectuadoDomain> _domain = new();
        private readonly PagoEfectuadoApplication _app;

        public PagoEfectuadoApplicationTests()
        {
            _app = new PagoEfectuadoApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Insertar_MapeaCabeceraFacturasYMedios_YDevuelveElEntry()
        {
            PagoEfectuado? recibido = null;
            List<PagoEfectuadoFactura>? facturasRecibidas = null;
            List<PagoEfectuadoMedio>? mediosRecibidos = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<PagoEfectuado>(), It.IsAny<IEnumerable<PagoEfectuadoFactura>>(), It.IsAny<IEnumerable<PagoEfectuadoMedio>>()))
                .Callback<PagoEfectuado, IEnumerable<PagoEfectuadoFactura>, IEnumerable<PagoEfectuadoMedio>>((p, f, m) => { recibido = p; facturasRecibidas = f.ToList(); mediosRecibidos = m.ToList(); })
                .ReturnsAsync(42);
            var dto = new PagoEfectuadoCrearDTO
            {
                Serie = 40, CodigoSn = "SN1", Comentario = "abono",
                Facturas = { new PagoEfectuadoFacturaCrearDTO { FacturaCompraEntry = 7, MontoAplicado = 60m } },
                Medios = { new PagoEfectuadoMedioCrearDTO { CodFormaPago = "CHQ", Monto = 60m, Referencia = "123", Banco = "Industrial" } }
            };

            var respuesta = await _app.InsertarAsync(dto);

            Assert.True(respuesta.Resultado);
            Assert.Equal(42, respuesta.Dato);
            Assert.Equal("Pago registrado correctamente.", respuesta.Mensaje);
            Assert.Equal("SN1", recibido!.CodigoSn);
            Assert.Equal(40, recibido.Serie);
            var factura = Assert.Single(facturasRecibidas!);
            Assert.Equal(7, factura.FacturaCompraEntry);
            Assert.Equal(60m, factura.MontoAplicado);
            var medio = Assert.Single(mediosRecibidos!);
            Assert.Equal("CHQ", medio.CodFormaPago);
            Assert.Equal("Industrial", medio.Banco);
        }

        [Fact]
        public async Task Insertar_ErrorDeDominio_DevuelveElMensajeSinLanzar()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<PagoEfectuado>(), It.IsAny<IEnumerable<PagoEfectuadoFactura>>(), It.IsAny<IEnumerable<PagoEfectuadoMedio>>()))
                .ThrowsAsync(new Exception("Indique al menos un documento a pagar."));

            var respuesta = await _app.InsertarAsync(new PagoEfectuadoCrearDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("Indique al menos un documento a pagar.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Cancelar_Exito_YNoEncontrado()
        {
            _domain.SetupSequence(d => d.CancelarAsync(7)).ReturnsAsync(true).ReturnsAsync(false);

            var ok = await _app.CancelarAsync(7);
            var noExiste = await _app.CancelarAsync(7);

            Assert.True(ok.Resultado);
            Assert.Equal("Pago cancelado correctamente.", ok.Mensaje);
            Assert.False(noExiste.Resultado);
            Assert.Equal("El pago efectuado no se encontró.", noExiste.Mensaje);
        }

        [Fact]
        public async Task Cancelar_YaCancelado_DevuelveElMensajeDelDominio()
        {
            _domain.Setup(d => d.CancelarAsync(7)).ThrowsAsync(new Exception("El pago ya está cancelado."));

            var respuesta = await _app.CancelarAsync(7);

            Assert.False(respuesta.Resultado);
            Assert.Equal("El pago ya está cancelado.", respuesta.Mensaje);
        }

        [Fact]
        public async Task ObtenerFacturas_MapeaNumeroTipoYTotalDelDocumento()
        {
            _domain.Setup(d => d.ObtenerFacturasAsync(7)).ReturnsAsync(new List<PagoEfectuadoFactura>
            {
                new() { Entry = 7, NoLinea = 1, FacturaCompraEntry = 3, MontoAplicado = 60m,
                        FacturaCompraNavigation = new FacturaCompra { Entry = 3, NumDoc = 30, DocSubType = "ND", TotalDoc = 150m } }
            });

            var respuesta = await _app.ObtenerFacturasAsync(7);

            var fila = Assert.Single(respuesta.Dato);
            Assert.Equal(30, fila.NumDoc);
            Assert.Equal("ND", fila.DocSubType);
            Assert.Equal(150m, fila.TotalDoc);
            Assert.Equal(60m, fila.MontoAplicado);
        }

        [Fact]
        public async Task ObtenerMedios_MapeaNombreYTipoDeLaFormaDePago()
        {
            _domain.Setup(d => d.ObtenerMediosAsync(7)).ReturnsAsync(new List<PagoEfectuadoMedio>
            {
                new() { Entry = 7, NoLinea = 1, CodFormaPago = "CHQ", Monto = 60m, Referencia = "123", Banco = "Industrial",
                        CodFormaPagoNavigation = new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C" } }
            });

            var respuesta = await _app.ObtenerMediosAsync(7);

            var fila = Assert.Single(respuesta.Dato);
            Assert.Equal("Cheque", fila.NombreFormaPago);
            Assert.Equal("C", fila.TipoDato);
            Assert.Equal("123", fila.Referencia);
        }

        [Fact]
        public async Task ObtenerAbiertos_MapeaDocumentoYSaldo()
        {
            var factura = new FacturaCompra { Entry = 3, NumDoc = 30, DocSubType = "--", FechaDoc = new DateTime(2026, 10, 1), TotalDoc = 100m };
            _domain.Setup(d => d.ObtenerAbiertosAsync("SN1")).ReturnsAsync(new List<DocumentoPorPagar>
            {
                new(factura, new SaldoFacturaCompra(3, 100m, 10m, 20m, 70m, "Parcial"))
            });

            var respuesta = await _app.ObtenerAbiertosAsync("SN1");

            var fila = Assert.Single(respuesta.Dato);
            Assert.Equal(3, fila.Entry);
            Assert.Equal(30, fila.NumDoc);
            Assert.Equal(100m, fila.Total);
            Assert.Equal(10m, fila.Acreditado);
            Assert.Equal(20m, fila.Pagado);
            Assert.Equal(70m, fila.Saldo);
            Assert.Equal("Parcial", fila.Estado);
        }
    }
}
