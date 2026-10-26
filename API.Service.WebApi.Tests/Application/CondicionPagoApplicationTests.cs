using API.Application.DTO.condicionPago;
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
    public class CondicionPagoApplicationTests
    {
        private readonly Mock<ICondicionPagoDomain> _domain = new();
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly CondicionPagoApplication _app;

        public CondicionPagoApplicationTests()
        {
            _app = new CondicionPagoApplication(_domain.Object, _mapper);
            _domain.Setup(d => d.InsertarAsync(It.IsAny<CondicionPago>())).ReturnsAsync(true);
            _domain.Setup(d => d.ActualizarAsync(It.IsAny<string>(), It.IsAny<CondicionPago>())).ReturnsAsync(true);
        }

        [Fact]
        public async Task InsertarAsync_DiasNulo_SeRechazaSinLlegarAlDominio()
        {
            var r = await _app.InsertarAsync(new CondicionPagoCrearDTO { Codigo = "C20", Nombre = "Crédito 20", Dias = null });

            Assert.False(r.Resultado);
            Assert.Equal("Los días de crédito son requeridos.", r.Mensaje);
            _domain.Verify(d => d.InsertarAsync(It.IsAny<CondicionPago>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_DiasNulo_SeRechazaSinLlegarAlDominio()
        {
            var r = await _app.ActualizarAsync("C30", new CondicionPagoActualizarDTO { Nombre = "Crédito 30", Dias = null });

            Assert.False(r.Resultado);
            Assert.Equal("Los días de crédito son requeridos.", r.Mensaje);
            _domain.Verify(d => d.ActualizarAsync(It.IsAny<string>(), It.IsAny<CondicionPago>()), Times.Never);
        }

        [Fact]
        public async Task InsertarYActualizar_ConDias_MapeanLosDiasAlDominio()
        {
            Assert.True((await _app.InsertarAsync(new CondicionPagoCrearDTO { Codigo = "C20", Nombre = "Crédito 20", Dias = 20 })).Resultado);
            Assert.True((await _app.ActualizarAsync("C30", new CondicionPagoActualizarDTO { Nombre = "Crédito 35", Dias = 0 })).Resultado);

            _domain.Verify(d => d.InsertarAsync(It.Is<CondicionPago>(c => c.Codigo == "C20" && c.Dias == 20)), Times.Once);
            _domain.Verify(d => d.ActualizarAsync("C30", It.Is<CondicionPago>(c => c.Dias == 0 && c.Nombre == "Crédito 35")), Times.Once);
        }
    }
}
