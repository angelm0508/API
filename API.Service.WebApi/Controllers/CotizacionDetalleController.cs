using API.Application.DTO;
using API.Application.DTO.cotizacion;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/CotizacionDetalle")]
    public class CotizacionDetalleController : ControllerBase
    {
        private readonly ICotizacionDetalleApplication _cotizacionDetalleApplication;

        public CotizacionDetalleController(ICotizacionDetalleApplication cotizacionDetalleApplication)
        {
            _cotizacionDetalleApplication = cotizacionDetalleApplication;
        }

        [RequierePermiso("Cotizaciones", Accion.Ver)]
        [HttpGet("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<CotizacionDetalleDTO>>> Obtener([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _cotizacionDetalleApplication.ObtenerAsync(entry, noLinea);

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

        [RequierePermiso("Cotizaciones", Accion.Ver)]
        [HttpGet("PorCotizacion/{entry:int}")]
        public async Task<ActionResult<Respuesta<IEnumerable<CotizacionDetalleDTO>>>> ObtenerPorCotizacion([FromRoute] int entry)
        {
            var detalles = await _cotizacionDetalleApplication.ObtenerPorCotizacionAsync(entry);

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [RequierePermiso("Cotizaciones", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<CotizacionDetalleDTO>>>> ObtenerTodoAsync()
        {
            var detalles = await _cotizacionDetalleApplication.ObtenerTodoAsync();

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [RequierePermiso("Cotizaciones", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] CotizacionDetalleCrearDTO obj)
        {
            var insert = await _cotizacionDetalleApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [RequierePermiso("Cotizaciones", Accion.Actualizar)]
        [HttpPut("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int entry, [FromRoute] int noLinea, [FromBody] CotizacionDetalleActualizarDTO obj)
        {
            var det = await _cotizacionDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var update = await _cotizacionDetalleApplication.ActualizarAsync(entry, noLinea, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [RequierePermiso("Cotizaciones", Accion.Eliminar)]
        [HttpDelete("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _cotizacionDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var delete = await _cotizacionDetalleApplication.EliminarAsync(entry, noLinea);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
