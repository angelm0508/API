using API.Application.DTO.usuario.usuario;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Usuario")]
    public class UsuarioController : ControllerBase
    {
        private readonly IUsuarioApplication _usuarioApplication;

        public UsuarioController(IUsuarioApplication usuarioApplication)
        {
            _usuarioApplication = usuarioApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<UsuarioDTO>> Obtener([FromRoute] int id)
        {
            var usuario = await _usuarioApplication.ObtenerAsync(id);

            if (!usuario.Resultado)
            {
                return BadRequest(new RespuestaError($"{usuario.Mensaje}"));
            }

            if (usuario.Dato == null)
            {
                return NotFound(new RespuestaError("El código del usuario no se encontró."));
            }

            return Ok(usuario.Dato);
        }

        [HttpGet("PorCodigo/{codigo}")]
        public async Task<ActionResult<UsuarioDTO>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var usuario = await _usuarioApplication.ObtenerAsync(codigo);

            if (!usuario.Resultado)
            {
                return BadRequest(new RespuestaError($"{usuario.Mensaje}"));
            }

            if (usuario.Dato == null)
            {
                return NotFound(new RespuestaError("El código del usuario no se encontró."));
            }

            return Ok(usuario.Dato);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<List<UsuarioDTO>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var usuarios = await _usuarioApplication.ObtenerContengaNombreAsync(name);

            if (!usuarios.Resultado)
            {
                return BadRequest(new RespuestaError(usuarios.Mensaje));
            }

            return Ok(usuarios.Dato);
        }

        [HttpGet]
        public async Task<ActionResult<List<UsuarioDTO>>> ObtenerTodoAsync()
        {
            var usuarios = await _usuarioApplication.ObtenerTodoAsync();

            if (!usuarios.Resultado)
            {
                return BadRequest(new RespuestaError(usuarios.Mensaje));
            }

            return Ok(usuarios.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> InsertarAsync([FromBody] UsuarioCrearDTO obj)
        {
            var insert = await _usuarioApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(new RespuestaError(insert.Mensaje));
            }

            return Ok();
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> ActualizarAsync([FromRoute] int id, [FromBody] UsuarioActualizarDTO obj)
        {
            var usuario = await _usuarioApplication.ObtenerAsync(id);

            if (usuario.Dato == null)
            {
                return NotFound(new RespuestaError("El código del usuario no se encontró."));
            }

            var update = await _usuarioApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(new RespuestaError(update.Mensaje));
            }

            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> EliminarAsync([FromRoute] int id)
        {
            var usuario = await _usuarioApplication.ObtenerAsync(id);

            if (usuario.Dato == null)
            {
                return NotFound(new RespuestaError("El código del usuario no se encontró."));
            }

            var delete = await _usuarioApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(new RespuestaError(delete.Mensaje));
            }

            return Ok();
        }
    }
}
