using API.Application.DTO.permiso;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class PermisoApplicationTests
    {
        private readonly Mock<IPermisoDomain> _permisoDomain = new();
        private readonly Mock<IModuloDomain> _moduloDomain = new();
        private readonly PermisoApplication _application;

        public PermisoApplicationTests()
        {
            _application = new PermisoApplication(_permisoDomain.Object, _moduloDomain.Object);
        }

        [Fact]
        public async Task ObtenerModulosAsync_Ok_MapeaElCatalogo()
        {
            _moduloDomain.Setup(m => m.ObtenerTodoAsync()).ReturnsAsync(new List<Modulo>
            {
                new() { Codigo = "Articulos", Nombre = "Artículos", Grupo = "Inventario" }
            }.AsAsyncQueryable());

            var respuesta = await _application.ObtenerModulosAsync();

            Assert.True(respuesta.Resultado);
            var m = Assert.Single(respuesta.Dato!);
            Assert.Equal("Articulos", m.Codigo);
        }

        [Fact]
        public async Task ObtenerMatrizUsuarioAsync_Ok_MapeaLaMatriz()
        {
            _permisoDomain.Setup(p => p.ObtenerMatrizUsuarioAsync(1))
                .ReturnsAsync(new List<PermisoModulo> { new("Articulos", true, false, false, false) });

            var respuesta = await _application.ObtenerMatrizUsuarioAsync(1);

            Assert.True(respuesta.Resultado);
            var fila = Assert.Single(respuesta.Dato!);
            Assert.Equal("Articulos", fila.CodigoModulo);
            Assert.True(fila.Ver);
        }

        [Fact]
        public async Task GuardarMatrizUsuarioAsync_Ok_DelegaAlDominio()
        {
            var matriz = new List<PermisoModuloDTO> { new() { CodigoModulo = "Articulos", Ver = true } };

            var respuesta = await _application.GuardarMatrizUsuarioAsync(1, matriz);

            Assert.True(respuesta.Resultado);
            _permisoDomain.Verify(p => p.GuardarMatrizUsuarioAsync(1, It.Is<List<PermisoModulo>>(
                l => l.Count == 1 && l[0].CodigoModulo == "Articulos" && l[0].Ver)), Times.Once);
        }

        [Fact]
        public async Task AplicarPlantillaAsync_Ok_DelegaAlDominio()
        {
            var respuesta = await _application.AplicarPlantillaAsync(1, 5);

            Assert.True(respuesta.Resultado);
            _permisoDomain.Verify(p => p.AplicarPlantillaAsync(1, 5), Times.Once);
        }

        [Fact]
        public async Task GuardarMatrizUsuarioAsync_ExcepcionDelDominio_DevuelveResultadoFalse()
        {
            _permisoDomain.Setup(p => p.GuardarMatrizUsuarioAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<PermisoModulo>>()))
                .ThrowsAsync(new Exception("fallo"));

            var respuesta = await _application.GuardarMatrizUsuarioAsync(1, new List<PermisoModuloDTO>());

            Assert.False(respuesta.Resultado);
            Assert.Equal("fallo", respuesta.Mensaje);
        }
    }
}
