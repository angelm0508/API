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
    public class PedidoApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<IPedidoDomain> _domain = new();
        private readonly PedidoApplication _app;

        public PedidoApplicationTests()
        {
            _app = new PedidoApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Insertar_ConLineas_UsaLaSobrecargaTransaccionalYMapeaLasLineas()
        {
            Pedido? recibido = null;
            List<PedidoDetalle>? lineasRecibidas = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Pedido>(), It.IsAny<IEnumerable<PedidoDetalle>>()))
                .Callback<Pedido, IEnumerable<PedidoDetalle>>((p, l) => { recibido = p; lineasRecibidas = l.ToList(); })
                .ReturnsAsync(42);
            var dto = new PedidoCrearDTO
            {
                Serie = 7, CodigoSn = "CL-1",
                Lineas = { new PedidoDetalleCrearDTO { CodArticulo = "ART1", Cantidad = 4m, Precio = 10m, BaseTipo = 3, BaseEntry = 12, BaseLinea = 1 } }
            };

            var r = await _app.InsertarAsync(dto);

            Assert.True(r.Resultado);
            Assert.Equal(42, r.Dato);
            Assert.Equal("CL-1", recibido!.CodigoSn);
            var l = Assert.Single(lineasRecibidas!);
            Assert.Equal(("ART1", 4m, 3, 12, 1), (l.CodArticulo, l.Cantidad, l.BaseTipo, l.BaseEntry, l.BaseLinea));
            _domain.Verify(d => d.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task Insertar_SinLineas_UsaElCaminoAntiguo()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Pedido>())).ReturnsAsync(9);

            var r = await _app.InsertarAsync(new PedidoCrearDTO { Serie = 7 });

            Assert.True(r.Resultado);
            Assert.Equal(9, r.Dato);
            _domain.Verify(d => d.InsertarAsync(It.IsAny<Pedido>(), It.IsAny<IEnumerable<PedidoDetalle>>()), Times.Never);
        }

        [Fact]
        public async Task Insertar_ConError_DevuelveElMensajeDelDominio()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Pedido>(), It.IsAny<IEnumerable<PedidoDetalle>>()))
                .ThrowsAsync(new Exception("La cantidad de la línea 1 de Cotización 12 excede la cantidad abierta (6)."));

            var r = await _app.InsertarAsync(new PedidoCrearDTO { Serie = 7, Lineas = { new PedidoDetalleCrearDTO { CodArticulo = "A", Cantidad = 9m } } });

            Assert.False(r.Resultado);
            Assert.Equal("La cantidad de la línea 1 de Cotización 12 excede la cantidad abierta (6).", r.Mensaje);
        }
    }
}
