using API.Application.DTO;
using API.Application.DTO.articulo.grupo_sn;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
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
        public async Task<ActionResult<Respuesta<GrupoSnDTO>>> Obtener([FromRoute] int id)
        {
            var grupoSn = await _grupoSnApplication.ObtenerAsync(id);

            if (!grupoSn.Resultado)
            {
                return BadRequest(grupoSn);
            }

            if (grupoSn.Dato == null)
            {
                grupoSn.Resultado = false;
                grupoSn.Mensaje = "El código del grupo SN no se encontró.";
                return NotFound(grupoSn);
            }

            return Ok(grupoSn);
        }

        [HttpGet("PorNombre/{name}")]
        public async Task<ActionResult<Respuesta<GrupoSnDTO>>> ObtenerPorNombre([FromRoute] string name)
        {
            var grupoSn = await _grupoSnApplication.ObtenerAsync(name);

            if (!grupoSn.Resultado)
            {
                return BadRequest(grupoSn);
            }

            if (grupoSn.Dato == null)
            {
                grupoSn.Resultado = false;
                grupoSn.Mensaje = "El nombre del grupo SN no se encontró.";
                return NotFound(grupoSn);
            }

            return Ok(grupoSn);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<Respuesta<IEnumerable<GrupoSnDTO>>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var gruposSn = await _grupoSnApplication.ObtenerContengaNombreAsync(name);

            if (!gruposSn.Resultado)
            {
                return BadRequest(gruposSn);
            }

            return Ok(gruposSn);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<GrupoSnDTO>>>> ObtenerTodoAsync()
        {
            var gruposSn = await _grupoSnApplication.ObtenerTodoAsync();

            if (!gruposSn.Resultado)
            {
                return BadRequest(gruposSn);
            }

            return Ok(gruposSn);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] GrupoSnCrearDTO obj)
        {
            var insert = await _grupoSnApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] GrupoSnActualizarDTO obj)
        {
            var grupoSn = await _grupoSnApplication.ObtenerAsync(id);

            if (grupoSn.Dato == null)
            {
                grupoSn.Resultado = false;
                grupoSn.Mensaje = "El código del grupo SN no se encontró.";
                return NotFound(grupoSn);
            }

            var update = await _grupoSnApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var grupoSn = await _grupoSnApplication.ObtenerAsync(id);

            if (grupoSn.Dato == null)
            {
                grupoSn.Resultado = false;
                grupoSn.Mensaje = "El código del grupo SN no se encontró.";
                return NotFound(grupoSn);
            }

            var delete = await _grupoSnApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
