using API.Application.DTO;
using API.Application.DTO.usuario.usuario;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        [RequierePermiso("Usuarios", Accion.Ver)]
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

        [RequierePermiso("Usuarios", Accion.Ver)]
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

        [RequierePermiso("Usuarios", Accion.Ver)]
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

        [RequierePermiso("Usuarios", Accion.Ver)]
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

        [RequierePermiso("Usuarios", Accion.Agregar)]
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

        [RequierePermiso("Usuarios", Accion.Actualizar)]
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

            var update = await _usuarioApplication.ActualizarAsync(id, obj, ObtenerUsuarioActualId());

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [RequierePermiso("Usuarios", Accion.Eliminar)]
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

            var delete = await _usuarioApplication.EliminarAsync(id, ObtenerUsuarioActualId());

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }

        /// <summary>
        /// Id del usuario autenticado (claim NameIdentifier del JWT). Null si el claim falta o no es
        /// numérico (p. ej. tokens anteriores): la auto-protección se omite en ese caso.
        /// </summary>
        private int? ObtenerUsuarioActualId()
        {
            var valor = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(valor, out var id) ? id : null;
        }
    }
}
