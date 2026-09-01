using API.Application.DTO;
using API.Application.DTO.entradaMercancia;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/EntradaMercanciaDetalle")]
    public class EntradaMercanciaDetalleController : ControllerBase
    {
        private readonly IEntradaMercanciaDetalleApplication _entradaMercanciaDetalleApplication;

        public EntradaMercanciaDetalleController(IEntradaMercanciaDetalleApplication entradaMercanciaDetalleApplication)
        {
            _entradaMercanciaDetalleApplication = entradaMercanciaDetalleApplication;
        }

        [HttpGet("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<EntradaMercanciaDetalleDTO>>> Obtener([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _entradaMercanciaDetalleApplication.ObtenerAsync(entry, noLinea);

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

        [HttpGet("PorEntradaMercancia/{entry:int}")]
        public async Task<ActionResult<Respuesta<IEnumerable<EntradaMercanciaDetalleDTO>>>> ObtenerPorEntradaMercancia([FromRoute] int entry)
        {
            var detalles = await _entradaMercanciaDetalleApplication.ObtenerPorEntradaMercanciaAsync(entry);

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<EntradaMercanciaDetalleDTO>>>> ObtenerTodoAsync()
        {
            var detalles = await _entradaMercanciaDetalleApplication.ObtenerTodoAsync();

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] EntradaMercanciaDetalleCrearDTO obj)
        {
            var insert = await _entradaMercanciaDetalleApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int entry, [FromRoute] int noLinea, [FromBody] EntradaMercanciaDetalleActualizarDTO obj)
        {
            var det = await _entradaMercanciaDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var update = await _entradaMercanciaDetalleApplication.ActualizarAsync(entry, noLinea, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _entradaMercanciaDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var delete = await _entradaMercanciaDetalleApplication.EliminarAsync(entry, noLinea);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
