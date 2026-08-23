using API.Application.DTO;
using API.Application.DTO.articulo.grupo_unidad_medida_det_articulo;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/GrupoUnidadMedidaDetArticulo")]
    public class GrupoUnidadMedidaDetArticuloController : ControllerBase
    {
        private readonly IGrupoUnidadMedidaDetArticuloApplication _grupoUnidadMedidaDetArticuloApplication;

        public GrupoUnidadMedidaDetArticuloController(IGrupoUnidadMedidaDetArticuloApplication grupoUnidadMedidaDetArticuloApplication)
        {
            _grupoUnidadMedidaDetArticuloApplication = grupoUnidadMedidaDetArticuloApplication;
        }

        [HttpGet("{grpMedidaEntry:int}/{numLinea:int}")]
        public async Task<ActionResult<Respuesta<GrupoUnidadMedidaDetArticuloDTO>>> Obtener([FromRoute] int grpMedidaEntry, [FromRoute] int numLinea)
        {
            var det = await _grupoUnidadMedidaDetArticuloApplication.ObtenerAsync(grpMedidaEntry, numLinea);

            if (!det.Resultado)
            {
                return BadRequest(det);
            }

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            return Ok(det);
        }

        [HttpGet("PorGrupo/{grpMedidaEntry:int}")]
        public async Task<ActionResult<Respuesta<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>>>> ObtenerPorGrupo([FromRoute] int grpMedidaEntry)
        {
            var detalles = await _grupoUnidadMedidaDetArticuloApplication.ObtenerPorGrupoAsync(grpMedidaEntry);

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<GrupoUnidadMedidaDetArticuloDTO>>>> ObtenerTodoAsync()
        {
            var detalles = await _grupoUnidadMedidaDetArticuloApplication.ObtenerTodoAsync();

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] GrupoUnidadMedidaDetArticuloCrearDTO obj)
        {
            var insert = await _grupoUnidadMedidaDetArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{grpMedidaEntry:int}/{numLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int grpMedidaEntry, [FromRoute] int numLinea, [FromBody] GrupoUnidadMedidaDetArticuloActualizarDTO obj)
        {
            var det = await _grupoUnidadMedidaDetArticuloApplication.ObtenerAsync(grpMedidaEntry, numLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var update = await _grupoUnidadMedidaDetArticuloApplication.ActualizarAsync(grpMedidaEntry, numLinea, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{grpMedidaEntry:int}/{numLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int grpMedidaEntry, [FromRoute] int numLinea)
        {
            var det = await _grupoUnidadMedidaDetArticuloApplication.ObtenerAsync(grpMedidaEntry, numLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var delete = await _grupoUnidadMedidaDetArticuloApplication.EliminarAsync(grpMedidaEntry, numLinea);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
