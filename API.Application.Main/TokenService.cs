using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace API.Application.Main
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly IPermisoDomain _permisoDomain;

        public TokenService(IConfiguration configuration, IPermisoDomain permisoDomain)
        {
            _configuration = configuration;
            _permisoDomain = permisoDomain;
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

        // Deny by default: un usuario (no SuperUsuario) solo recibe un claim "perm" por cada modulo con
        // al menos una letra marcada en su matriz. Sin filas en la matriz = sin acceso a ningun modulo.
        private async Task<List<Claim>> ArmarClaimsPermisoAsync(int usuarioId)
        {
            var matriz = await _permisoDomain.ObtenerMatrizUsuarioAsync(usuarioId);
            var claims = new List<Claim>();

            foreach (var m in matriz)
            {
                var letras = (m.Ver ? "V" : "") + (m.Agregar ? "A" : "") + (m.Actualizar ? "U" : "") + (m.Eliminar ? "E" : "");
                if (letras.Length > 0) claims.Add(new Claim("perm", $"{m.CodigoModulo}:{letras}"));
            }
            return claims;
        }
    }
}
