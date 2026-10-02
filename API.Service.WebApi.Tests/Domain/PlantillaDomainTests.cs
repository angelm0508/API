using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PlantillaDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Plantilla, int>> _repo = new();
        private readonly PlantillaDomain _domain;

        public PlantillaDomainTests()
        {
            _domain = new PlantillaDomain(_repo.Object);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveElIdAsignado()
        {
            _repo.Setup(r => r.InsertarAsync(It.IsAny<Plantilla>()))
                .ReturnsAsync((Plantilla p) => { p.Id = 7; return p; });

            var id = await _domain.InsertarAsync(new Plantilla { Nombre = "Vendedor" });

            Assert.Equal(7, id);
        }

        [Fact]
        public async Task ActualizarAsync_DelegaAlRepo()
        {
            _repo.Setup(r => r.ActualizarAsync(7, It.IsAny<Plantilla>())).ReturnsAsync(true);

            var resultado = await _domain.ActualizarAsync(7, new Plantilla { Nombre = "Vendedor 2" });

            Assert.True(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DelegaAlRepo()
        {
            _repo.Setup(r => r.EliminarAsync(7)).ReturnsAsync(true);

            var resultado = await _domain.EliminarAsync(7);

            Assert.True(resultado);
        }
    }
}
