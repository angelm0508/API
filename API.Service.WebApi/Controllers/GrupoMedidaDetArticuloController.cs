using API.Application.DTO;
using API.Application.DTO.articulo.grupo_medida_det_articulo;
using API.Application.Interface;
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
        public async Task<ActionResult<Respuesta<GrupoMedidaDetArticuloDTO>>> Obtener([FromRoute] int id)
        {
            var grupoMedidaDet = await _grupoMedidaDetArticuloApplication.ObtenerAsync(id);

            if (!grupoMedidaDet.Resultado)
            {
                return BadRequest(grupoMedidaDet);
            }

            if (grupoMedidaDet.Dato == null)
            {
                grupoMedidaDet.Resultado = false;
                grupoMedidaDet.Mensaje = "El código del detalle de grupo de medida no se encontró.";
                return NotFound(grupoMedidaDet);
            }

            return Ok(grupoMedidaDet);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<GrupoMedidaDetArticuloDTO>>>> ObtenerTodoAsync()
        {
            var gruposMedidaDet = await _grupoMedidaDetArticuloApplication.ObtenerTodoAsync();

            if (!gruposMedidaDet.Resultado)
            {
                return BadRequest(gruposMedidaDet);
            }

            return Ok(gruposMedidaDet);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] GrupoMedidaDetArticuloCrearDTO obj)
        {
            var insert = await _grupoMedidaDetArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] GrupoMedidaDetArticuloActualizarDTO obj)
        {
            var grupoMedidaDet = await _grupoMedidaDetArticuloApplication.ObtenerAsync(id);

            if (grupoMedidaDet.Dato == null)
            {
                grupoMedidaDet.Resultado = false;
                grupoMedidaDet.Mensaje = "El código del detalle de grupo de medida no se encontró.";
                return NotFound(grupoMedidaDet);
            }

            var update = await _grupoMedidaDetArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var grupoMedidaDet = await _grupoMedidaDetArticuloApplication.ObtenerAsync(id);

            if (grupoMedidaDet.Dato == null)
            {
                grupoMedidaDet.Resultado = false;
                grupoMedidaDet.Mensaje = "El código del detalle de grupo de medida no se encontró.";
                return NotFound(grupoMedidaDet);
            }

            var delete = await _grupoMedidaDetArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
