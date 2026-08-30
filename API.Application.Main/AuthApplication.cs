using API.Application.DTO;
using API.Application.DTO.autenticacion;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using Microsoft.AspNetCore.Identity;

namespace API.Application.Main
{
    public class AuthApplication : IAuthApplication
    {
        private readonly IUsuarioDomain _usuarioDomain;
        private readonly IPasswordHasher<Usuario> _passwordHasher;
        private readonly ITokenService _tokenService;

        public AuthApplication(IUsuarioDomain usuarioDomain, IPasswordHasher<Usuario> passwordHasher, ITokenService tokenService)
        {
            _usuarioDomain = usuarioDomain;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
        }

        #region async methods
        public async Task<Respuesta<LoginResponseDTO>> LoginAsync(LoginDTO obj)
        {
            var respuesta = new Respuesta<LoginResponseDTO>();
            try
            {
                var usuario = await _usuarioDomain.ObtenerAsync(obj.Usuario);

                if (usuario is null)
                {
                    respuesta.Resultado = false;
                    respuesta.Mensaje = "Usuario o contraseña incorrectos.";
                    return respuesta;
                }

                if (usuario.Bloqueado == "S")
                {
                    respuesta.Resultado = false;
                    respuesta.Mensaje = "El usuario está bloqueado.";
                    return respuesta;
                }

                if (string.IsNullOrEmpty(usuario.Password))
                {
                    respuesta.Resultado = false;
                    respuesta.Mensaje = "Usuario o contraseña incorrectos.";
                    return respuesta;
                }

                var verificacion = _passwordHasher.VerifyHashedPassword(usuario, usuario.Password, obj.Contrasena);

                if (verificacion == PasswordVerificationResult.Failed)
                {
                    respuesta.Resultado = false;
                    respuesta.Mensaje = "Usuario o contraseña incorrectos.";
                    return respuesta;
                }

                var (token, expira) = _tokenService.GenerarToken(usuario);

                respuesta.Resultado = true;
                respuesta.Mensaje = "Login exitoso.";
                respuesta.Dato = new LoginResponseDTO
                {
                    Resultado = true,
                    Mensaje = "Login exitoso.",
                    Token = token,
                    UsuarioNombre = usuario.Nombre ?? usuario.Codigo,
                    ExpirasEn = expira
                };
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion
    }
}
