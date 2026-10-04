using API.Application.DTO.autenticacion;
using API.Application.Interface;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class AuthApplicationTests
    {
        private readonly Mock<IUsuarioDomain> _domain = new();
        private readonly Mock<IPasswordHasher<Usuario>> _hasher = new();
        private readonly Mock<ITokenService> _token = new();
        private readonly AuthApplication _app;

        public AuthApplicationTests()
        {
            _app = new AuthApplication(_domain.Object, _hasher.Object, _token.Object);
        }

        [Fact]
        public async Task Login_UsuarioEliminado_RechazaConMensajeGenerico_SinVerificarPassword()
        {
            _domain.Setup(d => d.ObtenerAsync("jperez")).ReturnsAsync(new Usuario { Id = 1, Codigo = "jperez", Password = "HASH", Eliminado = "S" });

            var r = await _app.LoginAsync(new LoginDTO { Usuario = "jperez", Contrasena = "Clave1234" });

            Assert.False(r.Resultado);
            Assert.Equal("Usuario o contraseña incorrectos.", r.Mensaje);
            _hasher.Verify(h => h.VerifyHashedPassword(It.IsAny<Usuario>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _token.Verify(t => t.GenerarTokenAsync(It.IsAny<Usuario>()), Times.Never);
        }

        [Fact]
        public async Task Login_UsuarioEliminadoYBloqueado_RespondeLoMismoQueUnoInexistente()
        {
            _domain.Setup(d => d.ObtenerAsync("x")).ReturnsAsync(new Usuario { Id = 1, Codigo = "x", Bloqueado = "S", Eliminado = "S" });
            _domain.Setup(d => d.ObtenerAsync("nadie")).ReturnsAsync((Usuario)null!);

            var eliminado = await _app.LoginAsync(new LoginDTO { Usuario = "x", Contrasena = "p" });
            var inexistente = await _app.LoginAsync(new LoginDTO { Usuario = "nadie", Contrasena = "p" });

            Assert.Equal(inexistente.Mensaje, eliminado.Mensaje);
            Assert.Equal("Usuario o contraseña incorrectos.", eliminado.Mensaje);
        }

        [Fact]
        public async Task Login_UsuarioActivo_ConPasswordCorrecta_Autentica()
        {
            var u = new Usuario { Id = 1, Codigo = "jperez", Nombre = "Juan", Password = "HASH", Eliminado = "N", Bloqueado = "N" };
            _domain.Setup(d => d.ObtenerAsync("jperez")).ReturnsAsync(u);
            _hasher.Setup(h => h.VerifyHashedPassword(u, "HASH", "Clave1234")).Returns(PasswordVerificationResult.Success);
            _token.Setup(t => t.GenerarTokenAsync(u)).ReturnsAsync(("tok", DateTime.UtcNow.AddHours(1)));

            var r = await _app.LoginAsync(new LoginDTO { Usuario = "jperez", Contrasena = "Clave1234" });

            Assert.True(r.Resultado);
            Assert.Equal("tok", r.Dato.Token);
        }
    }
}
