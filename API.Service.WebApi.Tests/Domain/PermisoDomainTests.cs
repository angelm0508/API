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
        private readonly Mock<IRepositorioGenerico<Plantilla, int>> _repoPlantilla = new();
        private readonly Mock<IRepositorioGenerico<Modulo, string>> _repoModulo = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly PermisoDomain _domain;

        public PermisoDomainTests()
        {
            _domain = new PermisoDomain(_repoUsuarioPermiso.Object, _repoPlantillaDetalle.Object, _repoPlantilla.Object, _repoModulo.Object, _tx.Object);
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
        public async Task GuardarMatrizUsuarioAsync_BorraLasFilasViejasYInsertaLasNuevasConservandoLasTodoFalse()
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
                new("Usuarios", false, false, false, false), // todo-false: SE conserva ("sin acceso" persistible)
            };

            await _domain.GuardarMatrizUsuarioAsync(1, nuevaMatriz);

            _repoUsuarioPermiso.Verify(r => r.EliminarAsync(It.Is<(int, string)>(x => x.Item1 == 1 && x.Item2 == "Articulos")), Times.Once);
            _repoUsuarioPermiso.Verify(r => r.EliminarAsync(It.Is<(int, string)>(x => x.Item1 == 1 && x.Item2 == "Almacenes")), Times.Once);
            _repoUsuarioPermiso.Verify(r => r.AgregarSinGuardarAsync(It.Is<UsuarioPermiso>(p => p.CodigoModulo == "Articulos" && p.Agregar)), Times.Once);
            _repoUsuarioPermiso.Verify(r => r.AgregarSinGuardarAsync(It.Is<UsuarioPermiso>(p => p.CodigoModulo == "Usuarios" && !p.Ver && !p.Agregar && !p.Actualizar && !p.Eliminar)), Times.Once);
        }

        [Fact]
        public async Task GuardarMatrizUsuarioAsync_ListaVacia_BorraTodasLasFilasYNoInserta()
        {
            _repoUsuarioPermiso.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<UsuarioPermiso>
            {
                new() { UsuarioId = 1, CodigoModulo = "Articulos", Ver = true },
            }.AsAsyncQueryable());
            _repoUsuarioPermiso.Setup(r => r.EliminarAsync(It.IsAny<(int, string)>())).ReturnsAsync(true);

            await _domain.GuardarMatrizUsuarioAsync(1, new List<PermisoModulo>());

            _repoUsuarioPermiso.Verify(r => r.EliminarAsync(It.Is<(int, string)>(x => x.Item1 == 1 && x.Item2 == "Articulos")), Times.Once);
            _repoUsuarioPermiso.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<UsuarioPermiso>()), Times.Never);
        }

        [Fact]
        public async Task GuardarMatrizPlantillaAsync_SigueOmitiendoLasFilasTodoFalse()
        {
            _repoPlantillaDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PlantillaPermisoDetalle>().AsAsyncQueryable());
            _repoPlantillaDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<PlantillaPermisoDetalle>())).Returns(Task.CompletedTask);

            await _domain.GuardarMatrizPlantillaAsync(5, new List<PermisoModulo>
            {
                new("Articulos", true, false, false, false),
                new("Almacenes", false, false, false, false),
            });

            _repoPlantillaDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<PlantillaPermisoDetalle>(p => p.CodigoModulo == "Articulos")), Times.Once);
            _repoPlantillaDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<PlantillaPermisoDetalle>(p => p.CodigoModulo == "Almacenes")), Times.Never);
        }

        [Fact]
        public async Task AplicarPlantillaAsync_PlantillaInexistente_LanzaYNoTocaLaMatrizDelUsuario()
        {
            _repoPlantilla.Setup(r => r.ObtenerAsync(99)).ReturnsAsync((Plantilla?)null);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.AplicarPlantillaAsync(1, 99));

            Assert.Equal("La plantilla no existe.", ex.Message);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>()), Times.Never);
            _repoUsuarioPermiso.Verify(r => r.EliminarAsync(It.IsAny<(int, string)>()), Times.Never);
            _repoUsuarioPermiso.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<UsuarioPermiso>()), Times.Never);
        }

        [Fact]
        public async Task AplicarPlantillaAsync_PlantillaSinFilas_DejaTodosLosModulosEnTodoFalse()
        {
            _repoPlantilla.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Plantilla { Id = 5, Nombre = "Vacia" });
            _repoPlantillaDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PlantillaPermisoDetalle>().AsAsyncQueryable());
            _repoModulo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<Modulo>
            {
                new() { Codigo = "Articulos", Nombre = "Articulos", Grupo = "Inventario" },
                new() { Codigo = "Almacenes", Nombre = "Almacenes", Grupo = "Inventario" },
            }.AsAsyncQueryable());
            _repoUsuarioPermiso.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<UsuarioPermiso>().AsAsyncQueryable());
            _repoUsuarioPermiso.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<UsuarioPermiso>())).Returns(Task.CompletedTask);

            await _domain.AplicarPlantillaAsync(1, 5);

            _repoUsuarioPermiso.Verify(r => r.AgregarSinGuardarAsync(It.Is<UsuarioPermiso>(
                p => p.UsuarioId == 1 && !p.Ver && !p.Agregar && !p.Actualizar && !p.Eliminar)), Times.Exactly(2));
        }

        [Fact]
        public async Task AplicarPlantillaAsync_CopiaLaMatrizDeLaPlantillaAlUsuario()
        {
            _repoPlantilla.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Plantilla { Id = 5, Nombre = "Vendedor" });
            _repoModulo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<Modulo>
            {
                new() { Codigo = "Articulos", Nombre = "Articulos", Grupo = "Inventario" },
                new() { Codigo = "Almacenes", Nombre = "Almacenes", Grupo = "Inventario" },
            }.AsAsyncQueryable());
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
