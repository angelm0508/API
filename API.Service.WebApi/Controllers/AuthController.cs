using API.Application.DTO.autenticacion;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [ApiController]
    [Route("api/Auth")]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly IAuthApplication _authApplication;

        public AuthController(IAuthApplication authApplication)
        {
            _authApplication = authApplication;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDTO>> Login([FromBody] LoginDTO obj)
        {
            var resultado = await _authApplication.LoginAsync(obj);

            if (!resultado.Resultado)
                return BadRequest(new RespuestaError(resultado.Mensaje));

            return Ok(resultado.Dato);
        }
    }
}
