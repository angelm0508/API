using API.Application.DTO;
using API.Application.DTO.cotizacion;
using API.Application.Interface;
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
        public async Task<ActionResult<Respuesta<CotizacionDTO>>> Obtener([FromRoute] int id)
        {
            var cotizacion = await _cotizacionApplication.ObtenerAsync(id);

            if (!cotizacion.Resultado)
            {
                return BadRequest(cotizacion);
            }

            if (cotizacion.Dato == null)
            {
                cotizacion.Resultado = false;
                cotizacion.Mensaje = "El código de la cotización no se encontró.";
                return NotFound(cotizacion);
            }

            return Ok(cotizacion);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<CotizacionDTO>>>> ObtenerTodoAsync()
        {
            var cotizaciones = await _cotizacionApplication.ObtenerTodoAsync();

            if (!cotizaciones.Resultado)
            {
                return BadRequest(cotizaciones);
            }

            return Ok(cotizaciones);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] CotizacionCrearDTO obj)
        {
            var insert = await _cotizacionApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] CotizacionActualizarDTO obj)
        {
            var cotizacion = await _cotizacionApplication.ObtenerAsync(id);

            if (cotizacion.Dato == null)
            {
                cotizacion.Resultado = false;
                cotizacion.Mensaje = "El código de la cotización no se encontró.";
                return NotFound(cotizacion);
            }

            var update = await _cotizacionApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var cotizacion = await _cotizacionApplication.ObtenerAsync(id);

            if (cotizacion.Dato == null)
            {
                cotizacion.Resultado = false;
                cotizacion.Mensaje = "El código de la cotización no se encontró.";
                return NotFound(cotizacion);
            }

            var delete = await _cotizacionApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
