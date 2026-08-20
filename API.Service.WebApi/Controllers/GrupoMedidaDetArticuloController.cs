using API.Application.DTO.articulo.grupo_medida_det_articulo;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/GrupoMedidaDetArticulo")]
    public class GrupoMedidaDetArticuloController : ControllerBase
    {
        private readonly IGrupoMedidaDetArticuloApplication _grupoMedidaDetArticuloApplication;

        public GrupoMedidaDetArticuloController(IGrupoMedidaDetArticuloApplication grupoMedidaDetArticuloApplication)
        {
            _grupoMedidaDetArticuloApplication = grupoMedidaDetArticuloApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<GrupoMedidaDetArticuloDTO>> Obtener([FromRoute] int id)
        {
            var grupoMedidaDet = await _grupoMedidaDetArticuloApplication.ObtenerAsync(id);

            if (!grupoMedidaDet.Resultado)
            {
                return BadRequest(new RespuestaError($"{grupoMedidaDet.Mensaje}"));
            }

            if (grupoMedidaDet.Dato == null)
            {
                return NotFound(new RespuestaError("El código del detalle de grupo de medida no se encontró."));
            }

            return Ok(grupoMedidaDet.Dato);
        }

        [HttpGet]
        public async Task<ActionResult<List<GrupoMedidaDetArticuloDTO>>> ObtenerTodoAsync()
        {
            var gruposMedidaDet = await _grupoMedidaDetArticuloApplication.ObtenerTodoAsync();

            if (!gruposMedidaDet.Resultado)
            {
                return BadRequest(new RespuestaError(gruposMedidaDet.Mensaje));
            }

            return Ok(gruposMedidaDet.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> InsertarAsync([FromBody] GrupoMedidaDetArticuloCrearDTO obj)
        {
            var insert = await _grupoMedidaDetArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(new RespuestaError(insert.Mensaje));
            }

            return Ok();
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> ActualizarAsync([FromRoute] int id, [FromBody] GrupoMedidaDetArticuloActualizarDTO obj)
        {
            var grupoMedidaDet = await _grupoMedidaDetArticuloApplication.ObtenerAsync(id);

            if (grupoMedidaDet.Dato == null)
            {
                return NotFound(new RespuestaError("El código del detalle de grupo de medida no se encontró."));
            }

            var update = await _grupoMedidaDetArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(new RespuestaError(update.Mensaje));
            }

            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> EliminarAsync([FromRoute] int id)
        {
            var grupoMedidaDet = await _grupoMedidaDetArticuloApplication.ObtenerAsync(id);

            if (grupoMedidaDet.Dato == null)
            {
                return NotFound(new RespuestaError("El código del detalle de grupo de medida no se encontró."));
            }

            var delete = await _grupoMedidaDetArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(new RespuestaError(delete.Mensaje));
            }

            return Ok();
        }
    }
}
