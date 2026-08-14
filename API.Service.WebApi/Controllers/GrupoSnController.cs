using API.Application.DTO.articulo.grupo_sn;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [ApiController]
    [Route("api/GrupoSN")]
    public class GrupoSnController : ControllerBase
    {
        private readonly IGrupoSnApplication _grupoSnApplication;

        public GrupoSnController(IGrupoSnApplication grupoSnApplication)
        {
            _grupoSnApplication = grupoSnApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<GrupoSnDTO>> Obtener([FromRoute] int id)
        {
            var grupoSn = await _grupoSnApplication.ObtenerAsync(id);

            if (!grupoSn.Resultado)
            {
                return BadRequest(new RespuestaError($"{grupoSn.Mensaje}"));
            }

            if (grupoSn.Dato == null)
            {
                return NotFound(new RespuestaError("El código del grupo SN no se encontró."));
            }

            return Ok(grupoSn.Dato);
        }

        [HttpGet("PorNombre/{name}")]
        public async Task<ActionResult<GrupoSnDTO>> ObtenerPorNombre([FromRoute] string name)
        {
            var grupoSn = await _grupoSnApplication.ObtenerAsync(name);

            if (!grupoSn.Resultado)
            {
                return BadRequest(new RespuestaError($"{grupoSn.Mensaje}"));
            }

            if (grupoSn.Dato == null)
            {
                return NotFound(new RespuestaError("El nombre del grupo SN no se encontró."));
            }

            return Ok(grupoSn.Dato);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<List<GrupoSnDTO>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var gruposSn = await _grupoSnApplication.ObtenerContengaNombreAsync(name);

            if (!gruposSn.Resultado)
            {
                return BadRequest(new RespuestaError(gruposSn.Mensaje));
            }

            return Ok(gruposSn.Dato);
        }

        [HttpGet]
        public async Task<ActionResult<List<GrupoSnDTO>>> ObtenerTodoAsync()
        {
            var gruposSn = await _grupoSnApplication.ObtenerTodoAsync();

            if (!gruposSn.Resultado)
            {
                return BadRequest(new RespuestaError(gruposSn.Mensaje));
            }

            return Ok(gruposSn.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> InsertarAsync([FromBody] GrupoSnCrearDTO obj)
        {
            var insert = await _grupoSnApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(new RespuestaError(insert.Mensaje));
            }

            return Ok();
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> ActualizarAsync([FromRoute] int id, [FromBody] GrupoSnActualizarDTO obj)
        {
            var grupoSn = await _grupoSnApplication.ObtenerAsync(id);

            if (grupoSn.Dato == null)
            {
                return NotFound(new RespuestaError("El código del grupo SN no se encontró."));
            }

            var update = await _grupoSnApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(new RespuestaError(update.Mensaje));
            }

            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> EliminarAsync([FromRoute] int id)
        {
            var grupoSn = await _grupoSnApplication.ObtenerAsync(id);

            if (grupoSn.Dato == null)
            {
                return NotFound(new RespuestaError("El código del grupo SN no se encontró."));
            }

            var delete = await _grupoSnApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(new RespuestaError(delete.Mensaje));
            }

            return Ok();
        }
    }
}
