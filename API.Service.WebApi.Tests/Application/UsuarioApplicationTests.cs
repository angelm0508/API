using API.Application.DTO.usuario.usuario;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class UsuarioApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<IUsuarioDomain> _domain = new();
        private readonly UsuarioApplication _app;

        public UsuarioApplicationTests()
        {
            _app = new UsuarioApplication(_domain.Object, _mapper);
        }

        [Fact]
        public void UsuarioDTO_NoExponePasswordNiUltimaContra()
        {
            var propiedades = typeof(UsuarioDTO).GetProperties().Select(p => p.Name).ToList();

            Assert.DoesNotContain("Password", propiedades);
            Assert.DoesNotContain("UltimaContra", propiedades);
            Assert.Contains("Id", propiedades);
            Assert.Contains("Codigo", propiedades);
            Assert.Contains("LlaveInterna", propiedades);
        }

        [Fact]
        public async Task Insertar_PasaLaPasswordEnTextoPlanoAlDomain_YDevuelveElNuevoId()
        {
            Usuario? recibido = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Usuario>())).Callback<Usuario>(u => recibido = u).ReturnsAsync(8);

            var r = await _app.InsertarAsync(new UsuarioCrearDTO { Codigo = "jperez", Password = "Clave1234", Nombre = "Juan" });

            Assert.True(r.Resultado);
            Assert.Equal(8, r.Dato);
            Assert.Equal("Registro agregado correctamente.", r.Mensaje);
            Assert.Equal("Clave1234", recibido!.Password);
            Assert.Equal("jperez", recibido.Codigo);
        }

        [Fact]
        public async Task Insertar_ErrorDelDomain_DevuelveResultadoFalsoConElMensaje()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Usuario>())).ThrowsAsync(new Exception("Ya existe un usuario con el código: x"));

            var r = await _app.InsertarAsync(new UsuarioCrearDTO { Codigo = "x" });

            Assert.False(r.Resultado);
            Assert.Equal("Ya existe un usuario con el código: x", r.Mensaje);
        }

        [Fact]
        public async Task Actualizar_PasaIdYUsuarioActualAlDomain()
        {
            Usuario? recibido = null;
            _domain.Setup(d => d.ActualizarAsync(5, It.IsAny<Usuario>(), 9)).Callback<int, Usuario, int?>((_, u, _) => recibido = u).ReturnsAsync(true);

            var r = await _app.ActualizarAsync(5, new UsuarioActualizarDTO { Codigo = "u", Nombre = "N", Password = "", SuperUsuario = "S", Bloqueado = "N" }, 9);

            Assert.True(r.Resultado);
            Assert.True(r.Dato);
            Assert.Equal("Registro actualizado correctamente.", r.Mensaje);
            Assert.Equal("N", recibido!.Nombre);
            Assert.Equal("S", recibido.SuperUsuario);
            _domain.Verify(d => d.ActualizarAsync(5, It.IsAny<Usuario>(), 9), Times.Once);
        }

        [Fact]
        public async Task Actualizar_ErrorDelDomain_DevuelveResultadoFalsoConElMensaje()
        {
            _domain.Setup(d => d.ActualizarAsync(5, It.IsAny<Usuario>(), null)).ThrowsAsync(new Exception("El código de usuario no se puede modificar."));

            var r = await _app.ActualizarAsync(5, new UsuarioActualizarDTO { Codigo = "otro" }, null);

            Assert.False(r.Resultado);
            Assert.Equal("El código de usuario no se puede modificar.", r.Mensaje);
        }

        [Fact]
        public async Task Eliminar_PasaUsuarioActualAlDomain()
        {
            _domain.Setup(d => d.EliminarAsync(5, 9)).ReturnsAsync(true);

            var r = await _app.EliminarAsync(5, 9);

            Assert.True(r.Resultado);
            Assert.Equal("Registro eliminado correctamente.", r.Mensaje);
        }

        [Fact]
        public async Task Eliminar_ErrorDelDomain_DevuelveResultadoFalsoConElMensaje()
        {
            _domain.Setup(d => d.EliminarAsync(5, 5)).ThrowsAsync(new Exception("No puede eliminar su propio usuario."));

            var r = await _app.EliminarAsync(5, 5);

            Assert.False(r.Resultado);
            Assert.Equal("No puede eliminar su propio usuario.", r.Mensaje);
        }

        [Fact]
        public async Task Eliminar_DomainDevuelveFalse_ResultadoFalso()
        {
            _domain.Setup(d => d.EliminarAsync(5, null)).ReturnsAsync(false);

            var r = await _app.EliminarAsync(5, null);

            Assert.False(r.Resultado);
        }

        [Fact]
        public async Task ObtenerPorId_MapeaSinPassword()
        {
            _domain.Setup(d => d.ObtenerAsync(1)).ReturnsAsync(new Usuario
            {
                Id = 1,
                Codigo = "a",
                Nombre = "Ana Lopez",
                Email = "ana@x.com",
                SuperUsuario = "S",
                Bloqueado = "N",
                Eliminado = "N",
                Password = "HASH",
                UltimaContra = "x"
            });

            var r = await _app.ObtenerAsync(1);

            Assert.True(r.Resultado);
            Assert.Equal(1, r.Dato.Id);
            Assert.Equal("a", r.Dato.Codigo);
            Assert.Equal("Ana Lopez", r.Dato.Nombre);
            Assert.Equal("ana@x.com", r.Dato.Email);
            Assert.Equal("S", r.Dato.SuperUsuario);
            Assert.Equal("N", r.Dato.Bloqueado);

            var propiedades = r.Dato.GetType().GetProperties().Select(p => p.Name).ToList();
            Assert.DoesNotContain("Password", propiedades);
            Assert.DoesNotContain("UltimaContra", propiedades);
        }

        [Fact]
        public async Task ObtenerPorCodigo_UsuarioEliminado_SeComportaComoNoEncontrado()
        {
            _domain.Setup(d => d.ObtenerAsync("a")).ReturnsAsync(new Usuario { Id = 1, Codigo = "a", Eliminado = "S" });

            var r = await _app.ObtenerAsync("a");

            Assert.True(r.Resultado);
            Assert.Null(r.Dato);
        }

        [Fact]
        public async Task ObtenerPorCodigo_UsuarioActivo_Devuelve()
        {
            _domain.Setup(d => d.ObtenerAsync("a")).ReturnsAsync(new Usuario { Id = 1, Codigo = "a", Eliminado = "N" });

            var r = await _app.ObtenerAsync("a");

            Assert.Equal(1, r.Dato.Id);
        }

        [Fact]
        public async Task ObtenerTodo_MapeaLaListaDelDomain()
        {
            _domain.Setup(d => d.ObtenerTodoAsync()).ReturnsAsync(new List<Usuario>
            {
                new() { Id = 1, Codigo = "a" },
                new() { Id = 2, Codigo = "b" }
            }.AsAsyncQueryable());

            var r = await _app.ObtenerTodoAsync();

            Assert.True(r.Resultado);
            Assert.Equal(2, r.Dato.Count());
        }
    }
}
