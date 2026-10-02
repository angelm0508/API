using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class TokenServiceTests
    {
        private readonly Mock<IPermisoDomain> _permisoDomain = new();
        private readonly Mock<IModuloDomain> _moduloDomain = new();
        private readonly TokenService _service;

        public TokenServiceTests()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", "una-clave-de-prueba-bien-larga-para-firmar-el-token-1234567890" },
                { "Jwt:Issuer", "test" },
                { "Jwt:Audience", "test" },
                { "Jwt:ExpirationMinutes", "60" }
            }).Build();
            _service = new TokenService(config, _permisoDomain.Object, _moduloDomain.Object);
        }

        private static List<Claim> LeerClaims(string token) =>
            new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToList();

        [Fact]
        public async Task GenerarTokenAsync_SuperUsuario_NoEmiteClaimsPerm()
        {
            var usuario = new Usuario { Id = 1, Codigo = "admin", SuperUsuario = "S" };

            var (token, _) = await _service.GenerarTokenAsync(usuario);

            var claims = LeerClaims(token);
            Assert.Contains(claims, c => c.Type == "SuperUsuario" && c.Value == "S");
            Assert.DoesNotContain(claims, c => c.Type == "perm");
            _permisoDomain.Verify(p => p.ObtenerMatrizUsuarioAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GenerarTokenAsync_ConMatriz_EmiteUnClaimPorModuloConLetrasPresentes()
        {
            var usuario = new Usuario { Id = 2, Codigo = "vendedor", SuperUsuario = "N" };
            _permisoDomain.Setup(p => p.ObtenerMatrizUsuarioAsync(2)).ReturnsAsync(new List<PermisoModulo>
            {
                new("Articulos", true, true, false, false),
                new("Almacenes", false, false, false, false), // nada marcado -> no emite claim
            });

            var (token, _) = await _service.GenerarTokenAsync(usuario);

            var claims = LeerClaims(token);
            Assert.Contains(claims, c => c.Type == "perm" && c.Value == "Articulos:VA");
            Assert.DoesNotContain(claims, c => c.Type == "perm" && c.Value.StartsWith("Almacenes"));
        }

        [Fact]
        public async Task GenerarTokenAsync_SinNingunaFila_EmiteAccesoTotalATodoElCatalogo()
        {
            var usuario = new Usuario { Id = 3, Codigo = "nuevo", SuperUsuario = "N" };
            _permisoDomain.Setup(p => p.ObtenerMatrizUsuarioAsync(3)).ReturnsAsync(new List<PermisoModulo>());
            _moduloDomain.Setup(m => m.ObtenerTodoAsync()).ReturnsAsync(new List<Modulo>
            {
                new() { Codigo = "Articulos", Nombre = "Artículos", Grupo = "Inventario" },
                new() { Codigo = "Almacenes", Nombre = "Almacenes", Grupo = "Inventario" },
            }.AsAsyncQueryable());

            var (token, _) = await _service.GenerarTokenAsync(usuario);

            var claims = LeerClaims(token);
            Assert.Contains(claims, c => c.Type == "perm" && c.Value == "Articulos:VAUE");
            Assert.Contains(claims, c => c.Type == "perm" && c.Value == "Almacenes:VAUE");
        }

        [Fact]
        public async Task GenerarTokenAsync_SinNingunaFila_NoOtorgaPorDefectoElModuloUsuarios()
        {
            var usuario = new Usuario { Id = 4, Codigo = "nuevo", SuperUsuario = "N" };
            _permisoDomain.Setup(p => p.ObtenerMatrizUsuarioAsync(4)).ReturnsAsync(new List<PermisoModulo>());
            _moduloDomain.Setup(m => m.ObtenerTodoAsync()).ReturnsAsync(new List<Modulo>
            {
                new() { Codigo = "Articulos", Nombre = "Artículos", Grupo = "Inventario" },
                new() { Codigo = "Usuarios", Nombre = "Usuarios", Grupo = "Administración" },
            }.AsAsyncQueryable());

            var (token, _) = await _service.GenerarTokenAsync(usuario);

            var claims = LeerClaims(token);
            Assert.Contains(claims, c => c.Type == "perm" && c.Value == "Articulos:VAUE");
            Assert.DoesNotContain(claims, c => c.Type == "perm" && c.Value.StartsWith("Usuarios"));
        }

        [Fact]
        public async Task GenerarTokenAsync_ConFilasTodoFalse_NoEmitePermNiAccesoPorDefecto()
        {
            var usuario = new Usuario { Id = 5, Codigo = "bloqueado", SuperUsuario = "N" };
            _permisoDomain.Setup(p => p.ObtenerMatrizUsuarioAsync(5)).ReturnsAsync(new List<PermisoModulo>
            {
                new("Articulos", false, false, false, false),
                new("Almacenes", false, false, false, false),
            });

            var (token, _) = await _service.GenerarTokenAsync(usuario);

            Assert.DoesNotContain(LeerClaims(token), c => c.Type == "perm");
            _moduloDomain.Verify(m => m.ObtenerTodoAsync(), Times.Never);
        }
    }
}
