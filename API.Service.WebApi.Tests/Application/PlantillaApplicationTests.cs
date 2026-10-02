using API.Application.DTO.permiso;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class PlantillaApplicationTests
    {
        private readonly Mock<IPlantillaDomain> _domain = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly PlantillaApplication _application;

        public PlantillaApplicationTests()
        {
            _application = new PlantillaApplication(_domain.Object, _mapper.Object);
        }

        [Fact]
        public async Task InsertarAsync_Ok_DevuelveElId()
        {
            _mapper.Setup(m => m.Map<Plantilla>(It.IsAny<PlantillaCrearDTO>())).Returns(new Plantilla { Nombre = "Vendedor" });
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Plantilla>())).ReturnsAsync(7);

            var respuesta = await _application.InsertarAsync(new PlantillaCrearDTO { Nombre = "Vendedor" });

            Assert.True(respuesta.Resultado);
            Assert.Equal(7, respuesta.Dato);
        }

        [Fact]
        public async Task EliminarAsync_ExcepcionDelDominio_DevuelveResultadoFalse()
        {
            _domain.Setup(d => d.EliminarAsync(It.IsAny<int>())).ThrowsAsync(new Exception("fallo"));

            var respuesta = await _application.EliminarAsync(7);

            Assert.False(respuesta.Resultado);
            Assert.Equal("fallo", respuesta.Mensaje);
        }
    }
}
