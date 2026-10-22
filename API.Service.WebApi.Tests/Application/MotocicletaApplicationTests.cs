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
    public class MotocicletaApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<IMotocicletaDomain> _domain = new();
        private readonly MotocicletaApplication _app;

        public MotocicletaApplicationTests()
        {
            _app = new MotocicletaApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Obtener_MapeaElNombreDelCliente()
        {
            _domain.Setup(d => d.ObtenerAsync(5)).ReturnsAsync(new Motocicleta
            {
                Entry = 5, CodigoSn = "CL-1", Placa = "M123ABC", Activo = "S",
                CodigoSnNavigation = new SocioNegocio { Codigo = "CL-1", Nombre = "Cliente Uno" }
            });

            var respuesta = await _app.ObtenerAsync(5);

            Assert.Equal(("M123ABC", "Cliente Uno"), (respuesta.Dato.Placa, respuesta.Dato.NombreSn));
        }

        [Fact]
        public async Task ObtenerHistorial_MapeaLasOrdenesDeLaMoto()
        {
            _domain.Setup(d => d.ObtenerHistorialAsync(5)).ReturnsAsync(new[]
            {
                new OrdenServicio { Entry = 3, NumDoc = 30, EstadoServicio = "T", MotoEntry = 5, MecanicoNavigation = new Mecanico { Nombre = "Juan" } },
                new OrdenServicio { Entry = 1, NumDoc = 10, EstadoServicio = "E", MotoEntry = 5 }
            });

            var respuesta = await _app.ObtenerHistorialAsync(5);

            Assert.True(respuesta.Resultado);
            Assert.Equal(new[] { 30, 10 }, respuesta.Dato.Select(o => o.NumDoc));
            Assert.Equal("Juan", respuesta.Dato.First().NombreMecanico);
        }

        [Fact]
        public async Task ObtenerHistorial_ErrorDelDominio_DevuelveElMensaje()
        {
            _domain.Setup(d => d.ObtenerHistorialAsync(5)).ThrowsAsync(new Exception("falla"));

            var respuesta = await _app.ObtenerHistorialAsync(5);

            Assert.False(respuesta.Resultado);
            Assert.Equal("falla", respuesta.Mensaje);
        }
    }
}
