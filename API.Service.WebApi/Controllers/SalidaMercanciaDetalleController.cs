using API.Application.DTO;
using API.Application.DTO.salidaMercancia;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/SalidaMercanciaDetalle")]
    public class SalidaMercanciaDetalleController : ControllerBase
    {
        private readonly ISalidaMercanciaDetalleApplication _salidaMercanciaDetalleApplication;

        public SalidaMercanciaDetalleController(ISalidaMercanciaDetalleApplication salidaMercanciaDetalleApplication)
        {
            _salidaMercanciaDetalleApplication = salidaMercanciaDetalleApplication;
        }

        [HttpGet("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<SalidaMercanciaDetalleDTO>>> Obtener([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _salidaMercanciaDetalleApplication.ObtenerAsync(entry, noLinea);

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

        [HttpGet("PorSalidaMercancia/{entry:int}")]
        public async Task<ActionResult<Respuesta<IEnumerable<SalidaMercanciaDetalleDTO>>>> ObtenerPorSalidaMercancia([FromRoute] int entry)
        {
            var detalles = await _salidaMercanciaDetalleApplication.ObtenerPorSalidaMercanciaAsync(entry);

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<SalidaMercanciaDetalleDTO>>>> ObtenerTodoAsync()
        {
            var detalles = await _salidaMercanciaDetalleApplication.ObtenerTodoAsync();

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] SalidaMercanciaDetalleCrearDTO obj)
        {
            var insert = await _salidaMercanciaDetalleApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int entry, [FromRoute] int noLinea, [FromBody] SalidaMercanciaDetalleActualizarDTO obj)
        {
            var det = await _salidaMercanciaDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var update = await _salidaMercanciaDetalleApplication.ActualizarAsync(entry, noLinea, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _salidaMercanciaDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var delete = await _salidaMercanciaDetalleApplication.EliminarAsync(entry, noLinea);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
