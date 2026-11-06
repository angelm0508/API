using API.Application.DTO.entrega;
using API.Application.DTO.factura;
using API.Domain.Entity.Models;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    /// <summary>Mapeo de los campos de crédito de los DTO de creación de factura y entrega.</summary>
    public class FacturaCreditoMapeoTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();

        [Fact]
        public void Factura_PagoInmediatoDelDtoLlegaALaEntidad()
        {
            var entidad = _mapper.Map<Factura>(new FacturaCrearDTO { Serie = 4, TotalDoc = 500m, PagoInmediato = 200m, AutorizarExcesoCredito = true });

            Assert.Equal(200m, entidad.PagoInmediato);
        }

        [Fact]
        public void Factura_PagoInmediatoNuloLlegaComoCero()
        {
            var entidad = _mapper.Map<Factura>(new FacturaCrearDTO { Serie = 4, TotalDoc = 500m, PagoInmediato = null });

            Assert.Equal(0m, entidad.PagoInmediato);
        }

        [Fact]
        public void Entrega_LaBanderaDeAutorizacionNoRompeElMapeo()
        {
            var entidad = _mapper.Map<Entrega>(new EntregaCrearDTO { Serie = 4, CodigoSn = "CL-1", AutorizarExcesoCredito = true });

            Assert.Equal("CL-1", entidad.CodigoSn);
        }
    }
}
