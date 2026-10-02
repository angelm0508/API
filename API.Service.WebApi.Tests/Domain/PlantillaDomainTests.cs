using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PlantillaDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Plantilla, int>> _repo = new();
        private readonly Mock<IRepositorioGenerico<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)>> _repoDetalle = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly PlantillaDomain _domain;

        public PlantillaDomainTests()
        {
            _domain = new PlantillaDomain(_repo.Object, _repoDetalle.Object, _tx.Object);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
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
        public async Task EliminarAsync_BorraPrimeroElDetalleYLuegoElEncabezadoEnUnaTransaccion()
        {
            var orden = new List<string>();
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PlantillaPermisoDetalle>
            {
                new() { PlantillaId = 7, CodigoModulo = "Articulos", Ver = true },
                new() { PlantillaId = 7, CodigoModulo = "Almacenes", Ver = true },
                new() { PlantillaId = 8, CodigoModulo = "Articulos", Ver = true },
            }.AsAsyncQueryable());
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int, string)>()))
                .Callback<(int PlantillaId, string CodigoModulo)>(k => orden.Add($"detalle:{k.PlantillaId}:{k.CodigoModulo}"))
                .ReturnsAsync(true);
            _repo.Setup(r => r.EliminarAsync(7)).Callback(() => orden.Add("plantilla:7")).ReturnsAsync(true);

            var resultado = await _domain.EliminarAsync(7);

            Assert.True(resultado);
            Assert.Equal(new[] { "detalle:7:Articulos", "detalle:7:Almacenes", "plantilla:7" }, orden);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int, string)>(x => x.Item1 == 8)), Times.Never);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>()), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_SinDetalle_SoloBorraElEncabezado()
        {
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PlantillaPermisoDetalle>().AsAsyncQueryable());
            _repo.Setup(r => r.EliminarAsync(7)).ReturnsAsync(true);

            var resultado = await _domain.EliminarAsync(7);

            Assert.True(resultado);
            _repoDetalle.Verify(r => r.EliminarAsync(It.IsAny<(int, string)>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_SiFallaElEncabezado_PropagaLaExcepcion()
        {
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PlantillaPermisoDetalle>().AsAsyncQueryable());
            _repo.Setup(r => r.EliminarAsync(7)).ThrowsAsync(new InvalidOperationException("fk"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _domain.EliminarAsync(7));
        }
    }
}
