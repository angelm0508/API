using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace API.Application.Main
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly IPermisoDomain _permisoDomain;
        private readonly IModuloDomain _moduloDomain;

        public TokenService(IConfiguration configuration, IPermisoDomain permisoDomain, IModuloDomain moduloDomain)
        {
            _configuration = configuration;
            _permisoDomain = permisoDomain;
            _moduloDomain = moduloDomain;
        }

        public async Task<(string Token, DateTime ExpiraEn)> GenerarTokenAsync(Usuario usuario)
        {
            var key = _configuration["Jwt:Key"];
            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];
            var expirationMinutes = int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var minutos) ? minutos : 60;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new(ClaimTypes.Name, usuario.Codigo),
                new("SuperUsuario", usuario.SuperUsuario ?? "N")
            };

            if (usuario.SuperUsuario != "S")
            {
                claims.AddRange(await ArmarClaimsPermisoAsync(usuario.Id));
            }

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var expiraEn = DateTime.UtcNow.AddMinutes(expirationMinutes);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiraEn,
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return (tokenString, expiraEn);
        }

        // Sin fila en la matriz = acceso total (decision de la spec): se emite un claim "perm"
        // con las 4 letras por cada modulo del catalogo excepto "Usuarios".
        private async Task<List<Claim>> ArmarClaimsPermisoAsync(int usuarioId)
        {
            var matriz = await _permisoDomain.ObtenerMatrizUsuarioAsync(usuarioId);
            var claims = new List<Claim>();

            if (matriz.Count == 0)
            {
                // "Usuarios" (administración de usuarios/permisos) es exclusivo de SuperUsuario: nunca se otorga por defecto.
                var modulos = await (await _moduloDomain.ObtenerTodoAsync()).Where(m => m.Codigo != "Usuarios").ToListAsync();
                foreach (var m in modulos)
                    claims.Add(new Claim("perm", $"{m.Codigo}:VAUE"));
                return claims;
            }

            foreach (var m in matriz)
            {
                var letras = (m.Ver ? "V" : "") + (m.Agregar ? "A" : "") + (m.Actualizar ? "U" : "") + (m.Eliminar ? "E" : "");
                if (letras.Length > 0) claims.Add(new Claim("perm", $"{m.CodigoModulo}:{letras}"));
            }
            return claims;
        }
    }
}
