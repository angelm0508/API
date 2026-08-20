using API.Application.DTO.articulo.grupo_medida_articulo;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/GrupoMedidaArticulo")]
    public class GrupoMedidaArticuloController : ControllerBase
    {
        private readonly IGrupoMedidaArticuloApplication _grupoMedidaArticuloApplication;

        public GrupoMedidaArticuloController(IGrupoMedidaArticuloApplication grupoMedidaArticuloApplication)
        {
            _grupoMedidaArticuloApplication = grupoMedidaArticuloApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<GrupoMedidaArticuloDTO>> Obtener([FromRoute] int id)
        {
            var grupoMedida = await _grupoMedidaArticuloApplication.ObtenerAsync(id);

            if (!grupoMedida.Resultado)
            {
                return BadRequest(new RespuestaError($"{grupoMedida.Mensaje}"));
            }

            if (grupoMedida.Dato == null)
            {
                return NotFound(new RespuestaError("El código del grupo de medida no se encontró."));
            }

            return Ok(grupoMedida.Dato);
        }

        [HttpGet("PorNombre/{name}")]
        public async Task<ActionResult<GrupoMedidaArticuloDTO>> ObtenerPorNombre([FromRoute] string name)
        {
            var grupoMedida = await _grupoMedidaArticuloApplication.ObtenerAsync(name);

            if (!grupoMedida.Resultado)
            {
                return BadRequest(new RespuestaError($"{grupoMedida.Mensaje}"));
            }

            if (grupoMedida.Dato == null)
            {
                return NotFound(new RespuestaError("El nombre del grupo de medida no se encontró."));
            }

            return Ok(grupoMedida.Dato);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<List<GrupoMedidaArticuloDTO>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var gruposMedida = await _grupoMedidaArticuloApplication.ObtenerContengaNombreAsync(name);

            if (!gruposMedida.Resultado)
            {
                return BadRequest(new RespuestaError(gruposMedida.Mensaje));
            }

            return Ok(gruposMedida.Dato);
        }

        [HttpGet]
        public async Task<ActionResult<List<GrupoMedidaArticuloDTO>>> ObtenerTodoAsync()
        {
            var gruposMedida = await _grupoMedidaArticuloApplication.ObtenerTodoAsync();

            if (!gruposMedida.Resultado)
            {
                return BadRequest(new RespuestaError(gruposMedida.Mensaje));
            }

            return Ok(gruposMedida.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> InsertarAsync([FromBody] GrupoMedidaArticuloCrearDTO obj)
        {
            var insert = await _grupoMedidaArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(new RespuestaError(insert.Mensaje));
            }

            return Ok();
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> ActualizarAsync([FromRoute] int id, [FromBody] GrupoMedidaArticuloActualizarDTO obj)
        {
            var grupoMedida = await _grupoMedidaArticuloApplication.ObtenerAsync(id);

            if (grupoMedida.Dato == null)
            {
                return NotFound(new RespuestaError("El código del grupo de medida no se encontró."));
            }

            var update = await _grupoMedidaArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(new RespuestaError(update.Mensaje));
            }

            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> EliminarAsync([FromRoute] int id)
        {
            var grupoMedida = await _grupoMedidaArticuloApplication.ObtenerAsync(id);

            if (grupoMedida.Dato == null)
            {
                return NotFound(new RespuestaError("El código del grupo de medida no se encontró."));
            }

            var delete = await _grupoMedidaArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(new RespuestaError(delete.Mensaje));
            }

            return Ok();
        }
    }
}
