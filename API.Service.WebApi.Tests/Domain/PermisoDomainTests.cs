using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PermisoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<UsuarioPermiso, (int UsuarioId, string CodigoModulo)>> _repoUsuarioPermiso = new();
        private readonly Mock<IRepositorioGenerico<PlantillaPermisoDetalle, (int PlantillaId, string CodigoModulo)>> _repoPlantillaDetalle = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly PermisoDomain _domain;

        public PermisoDomainTests()
        {
            _domain = new PermisoDomain(_repoUsuarioPermiso.Object, _repoPlantillaDetalle.Object, _tx.Object);
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
        }

        [Fact]
        public async Task ObtenerMatrizUsuarioAsync_DevuelveLasFilasDelUsuario()
        {
            _repoUsuarioPermiso.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<UsuarioPermiso>
            {
                new() { UsuarioId = 1, CodigoModulo = "Articulos", Ver = true, Agregar = true },
                new() { UsuarioId = 2, CodigoModulo = "Articulos", Ver = true },
            }.AsAsyncQueryable());

            var matriz = await _domain.ObtenerMatrizUsuarioAsync(1);

            var fila = Assert.Single(matriz);
            Assert.Equal("Articulos", fila.CodigoModulo);
            Assert.True(fila.Ver);
            Assert.True(fila.Agregar);
            Assert.False(fila.Actualizar);
        }

        [Fact]
        public async Task ObtenerMatrizUsuarioAsync_SinFilas_DevuelveListaVacia()
        {
            _repoUsuarioPermiso.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<UsuarioPermiso>().AsAsyncQueryable());

            var matriz = await _domain.ObtenerMatrizUsuarioAsync(1);

            Assert.Empty(matriz);
        }

        [Fact]
        public async Task GuardarMatrizUsuarioAsync_BorraLasFilasViejasYInsertaLasNuevas()
        {
            _repoUsuarioPermiso.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<UsuarioPermiso>
            {
                new() { UsuarioId = 1, CodigoModulo = "Articulos", Ver = true },
                new() { UsuarioId = 1, CodigoModulo = "Almacenes", Ver = true },
            }.AsAsyncQueryable());
            _repoUsuarioPermiso.Setup(r => r.EliminarAsync(It.IsAny<(int, string)>())).ReturnsAsync(true);
            _repoUsuarioPermiso.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<UsuarioPermiso>())).Returns(Task.CompletedTask);

            var nuevaMatriz = new List<PermisoModulo>
            {
                new("Articulos", true, true, false, false),
                new("Usuarios", false, false, false, false), // las 4 en false -> no se inserta
            };

            await _domain.GuardarMatrizUsuarioAsync(1, nuevaMatriz);

            _repoUsuarioPermiso.Verify(r => r.EliminarAsync(It.Is<(int, string)>(x => x.Item1 == 1 && x.Item2 == "Articulos")), Times.Once);
            _repoUsuarioPermiso.Verify(r => r.EliminarAsync(It.Is<(int, string)>(x => x.Item1 == 1 && x.Item2 == "Almacenes")), Times.Once);
            _repoUsuarioPermiso.Verify(r => r.AgregarSinGuardarAsync(It.Is<UsuarioPermiso>(p => p.CodigoModulo == "Articulos" && p.Agregar)), Times.Once);
            _repoUsuarioPermiso.Verify(r => r.AgregarSinGuardarAsync(It.Is<UsuarioPermiso>(p => p.CodigoModulo == "Usuarios")), Times.Never);
        }

        [Fact]
        public async Task AplicarPlantillaAsync_CopiaLaMatrizDeLaPlantillaAlUsuario()
        {
            _repoUsuarioPermiso.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<UsuarioPermiso>
            {
                new() { UsuarioId = 1, CodigoModulo = "Almacenes", Ver = true },
            }.AsAsyncQueryable());
            _repoPlantillaDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PlantillaPermisoDetalle>
            {
                new() { PlantillaId = 5, CodigoModulo = "Articulos", Ver = true, Agregar = true, Actualizar = true, Eliminar = true },
            }.AsAsyncQueryable());
            _repoUsuarioPermiso.Setup(r => r.EliminarAsync(It.IsAny<(int, string)>())).ReturnsAsync(true);
            _repoUsuarioPermiso.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<UsuarioPermiso>())).Returns(Task.CompletedTask);

            await _domain.AplicarPlantillaAsync(1, 5);

            _repoUsuarioPermiso.Verify(r => r.EliminarAsync(It.Is<(int, string)>(x => x.Item1 == 1 && x.Item2 == "Almacenes")), Times.Once);
            _repoUsuarioPermiso.Verify(r => r.AgregarSinGuardarAsync(It.Is<UsuarioPermiso>(
                p => p.UsuarioId == 1 && p.CodigoModulo == "Articulos" && p.Ver && p.Agregar && p.Actualizar && p.Eliminar)), Times.Once);
        }
    }
}
