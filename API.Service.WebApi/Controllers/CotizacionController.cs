using API.Application.DTO.cotizacion;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Cotizacion")]
    public class CotizacionController : ControllerBase
    {
        private readonly ICotizacionApplication _cotizacionApplication;

        public CotizacionController(ICotizacionApplication cotizacionApplication)
        {
            _cotizacionApplication = cotizacionApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CotizacionDTO>> Obtener([FromRoute] int id)
        {
            var cotizacion = await _cotizacionApplication.ObtenerAsync(id);

            if (!cotizacion.Resultado)
            {
                return BadRequest(new RespuestaError($"{cotizacion.Mensaje}"));
            }

            if (cotizacion.Dato == null)
            {
                return NotFound(new RespuestaError("El código de la cotización no se encontró."));
            }

            return Ok(cotizacion.Dato);
        }

        [HttpGet]
        public async Task<ActionResult<List<CotizacionDTO>>> ObtenerTodoAsync()
        {
            var cotizaciones = await _cotizacionApplication.ObtenerTodoAsync();

            if (!cotizaciones.Resultado)
            {
                return BadRequest(new RespuestaError(cotizaciones.Mensaje));
            }

            return Ok(cotizaciones.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> InsertarAsync([FromBody] CotizacionCrearDTO obj)
        {
            var insert = await _cotizacionApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(new RespuestaError(insert.Mensaje));
            }

            return Ok();
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> ActualizarAsync([FromRoute] int id, [FromBody] CotizacionActualizarDTO obj)
        {
            var cotizacion = await _cotizacionApplication.ObtenerAsync(id);

            if (cotizacion.Dato == null)
            {
                return NotFound(new RespuestaError("El código de la cotización no se encontró."));
            }

            var update = await _cotizacionApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(new RespuestaError(update.Mensaje));
            }

            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> EliminarAsync([FromRoute] int id)
        {
            var cotizacion = await _cotizacionApplication.ObtenerAsync(id);

            if (cotizacion.Dato == null)
            {
                return NotFound(new RespuestaError("El código de la cotización no se encontró."));
            }

            var delete = await _cotizacionApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(new RespuestaError(delete.Mensaje));
            }

            return Ok();
        }
    }
}
