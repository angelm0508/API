using API.Application.DTO;
using API.Application.DTO.usuario.usuario;
using API.Application.Interface;
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
        public async Task<ActionResult<Respuesta<UsuarioDTO>>> Obtener([FromRoute] int id)
        {
            var usuario = await _usuarioApplication.ObtenerAsync(id);

            if (!usuario.Resultado)
            {
                return BadRequest(usuario);
            }

            if (usuario.Dato == null)
            {
                usuario.Resultado = false;
                usuario.Mensaje = "El código del usuario no se encontró.";
                return NotFound(usuario);
            }

            return Ok(usuario);
        }

        [HttpGet("PorCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<UsuarioDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var usuario = await _usuarioApplication.ObtenerAsync(codigo);

            if (!usuario.Resultado)
            {
                return BadRequest(usuario);
            }

            if (usuario.Dato == null)
            {
                usuario.Resultado = false;
                usuario.Mensaje = "El código del usuario no se encontró.";
                return NotFound(usuario);
            }

            return Ok(usuario);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<Respuesta<IEnumerable<UsuarioDTO>>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var usuarios = await _usuarioApplication.ObtenerContengaNombreAsync(name);

            if (!usuarios.Resultado)
            {
                return BadRequest(usuarios);
            }

            return Ok(usuarios);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<UsuarioDTO>>>> ObtenerTodoAsync()
        {
            var usuarios = await _usuarioApplication.ObtenerTodoAsync();

            if (!usuarios.Resultado)
            {
                return BadRequest(usuarios);
            }

            return Ok(usuarios);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] UsuarioCrearDTO obj)
        {
            var insert = await _usuarioApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] UsuarioActualizarDTO obj)
        {
            var usuario = await _usuarioApplication.ObtenerAsync(id);

            if (usuario.Dato == null)
            {
                usuario.Resultado = false;
                usuario.Mensaje = "El código del usuario no se encontró.";
                return NotFound(usuario);
            }

            var update = await _usuarioApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var usuario = await _usuarioApplication.ObtenerAsync(id);

            if (usuario.Dato == null)
            {
                usuario.Resultado = false;
                usuario.Mensaje = "El código del usuario no se encontró.";
                return NotFound(usuario);
            }

            var delete = await _usuarioApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
