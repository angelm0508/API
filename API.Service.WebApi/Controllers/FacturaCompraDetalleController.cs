using API.Application.DTO;
using API.Application.DTO.facturaCompra;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/FacturaCompraDetalle")]
    public class FacturaCompraDetalleController : ControllerBase
    {
        private readonly IFacturaCompraDetalleApplication _facturaCompraDetalleApplication;

        public FacturaCompraDetalleController(IFacturaCompraDetalleApplication facturaCompraDetalleApplication)
        {
            _facturaCompraDetalleApplication = facturaCompraDetalleApplication;
        }

        [HttpGet("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<FacturaCompraDetalleDTO>>> Obtener([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _facturaCompraDetalleApplication.ObtenerAsync(entry, noLinea);

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

        [HttpGet("PorFacturaCompra/{entry:int}")]
        public async Task<ActionResult<Respuesta<IEnumerable<FacturaCompraDetalleDTO>>>> ObtenerPorFacturaCompra([FromRoute] int entry)
        {
            var detalles = await _facturaCompraDetalleApplication.ObtenerPorFacturaCompraAsync(entry);

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<FacturaCompraDetalleDTO>>>> ObtenerTodoAsync()
        {
            var detalles = await _facturaCompraDetalleApplication.ObtenerTodoAsync();

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] FacturaCompraDetalleCrearDTO obj)
        {
            var insert = await _facturaCompraDetalleApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int entry, [FromRoute] int noLinea, [FromBody] FacturaCompraDetalleActualizarDTO obj)
        {
            var det = await _facturaCompraDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var update = await _facturaCompraDetalleApplication.ActualizarAsync(entry, noLinea, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _facturaCompraDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var delete = await _facturaCompraDetalleApplication.EliminarAsync(entry, noLinea);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
