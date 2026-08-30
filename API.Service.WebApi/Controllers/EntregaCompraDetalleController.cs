using API.Application.DTO;
using API.Application.DTO.entregaCompra;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/EntregaCompraDetalle")]
    public class EntregaCompraDetalleController : ControllerBase
    {
        private readonly IEntregaCompraDetalleApplication _entregaCompraDetalleApplication;

        public EntregaCompraDetalleController(IEntregaCompraDetalleApplication entregaCompraDetalleApplication)
        {
            _entregaCompraDetalleApplication = entregaCompraDetalleApplication;
        }

        [HttpGet("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<EntregaCompraDetalleDTO>>> Obtener([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _entregaCompraDetalleApplication.ObtenerAsync(entry, noLinea);

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

        [HttpGet("PorEntregaCompra/{entry:int}")]
        public async Task<ActionResult<Respuesta<IEnumerable<EntregaCompraDetalleDTO>>>> ObtenerPorEntregaCompra([FromRoute] int entry)
        {
            var detalles = await _entregaCompraDetalleApplication.ObtenerPorEntregaCompraAsync(entry);

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<EntregaCompraDetalleDTO>>>> ObtenerTodoAsync()
        {
            var detalles = await _entregaCompraDetalleApplication.ObtenerTodoAsync();

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] EntregaCompraDetalleCrearDTO obj)
        {
            var insert = await _entregaCompraDetalleApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int entry, [FromRoute] int noLinea, [FromBody] EntregaCompraDetalleActualizarDTO obj)
        {
            var det = await _entregaCompraDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var update = await _entregaCompraDetalleApplication.ActualizarAsync(entry, noLinea, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _entregaCompraDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var delete = await _entregaCompraDetalleApplication.EliminarAsync(entry, noLinea);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
