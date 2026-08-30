using API.Application.DTO;
using API.Application.DTO.numeracion.numeracion_documento_det;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/NumeracionDocumentoDet")]
    public class NumeracionDocumentoDetController : ControllerBase
    {
        private readonly INumeracionDocumentoDetApplication _numeracionDocumentoDetApplication;

        public NumeracionDocumentoDetController(INumeracionDocumentoDetApplication numeracionDocumentoDetApplication)
        {
            _numeracionDocumentoDetApplication = numeracionDocumentoDetApplication;
        }

        [HttpGet("{serie:int}")]
        public async Task<ActionResult<Respuesta<NumeracionDocumentoDetDTO>>> Obtener([FromRoute] int serie)
        {
            var numeracionDet = await _numeracionDocumentoDetApplication.ObtenerAsync(serie);

            if (!numeracionDet.Resultado)
            {
                return BadRequest(numeracionDet);
            }

            if (numeracionDet.Dato == null)
            {
                numeracionDet.Resultado = false;
                numeracionDet.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(numeracionDet);
            }

            return Ok(numeracionDet);
        }

        [HttpGet("PorDocumento/{codigoObj}")]
        public async Task<ActionResult<Respuesta<IEnumerable<NumeracionDocumentoDetDTO>>>> ObtenerPorDocumento([FromRoute] string codigoObj)
        {
            var numeracionDets = await _numeracionDocumentoDetApplication.ObtenerPorDocumentoAsync(codigoObj);

            if (!numeracionDets.Resultado)
            {
                return BadRequest(numeracionDets);
            }

            return Ok(numeracionDets);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<NumeracionDocumentoDetDTO>>>> ObtenerTodoAsync()
        {
            var numeracionDets = await _numeracionDocumentoDetApplication.ObtenerTodoAsync();

            if (!numeracionDets.Resultado)
            {
                return BadRequest(numeracionDets);
            }

            return Ok(numeracionDets);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] NumeracionDocumentoDetCrearDTO obj)
        {
            var insert = await _numeracionDocumentoDetApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{serie:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int serie, [FromBody] NumeracionDocumentoDetActualizarDTO obj)
        {
            var numeracionDet = await _numeracionDocumentoDetApplication.ObtenerAsync(serie);

            if (numeracionDet.Dato == null)
            {
                numeracionDet.Resultado = false;
                numeracionDet.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(numeracionDet);
            }

            var update = await _numeracionDocumentoDetApplication.ActualizarAsync(serie, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{serie:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int serie)
        {
            var numeracionDet = await _numeracionDocumentoDetApplication.ObtenerAsync(serie);

            if (numeracionDet.Dato == null)
            {
                numeracionDet.Resultado = false;
                numeracionDet.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(numeracionDet);
            }

            var delete = await _numeracionDocumentoDetApplication.EliminarAsync(serie);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }

        [HttpPost("GenerarCodigo/{serie:int}")]
        public async Task<ActionResult<Respuesta<string>>> GenerarCodigoAsync([FromRoute] int serie)
        {
            var generado = await _numeracionDocumentoDetApplication.GenerarCodigoAsync(serie);

            if (!generado.Resultado)
            {
                return BadRequest(generado);
            }

            return Ok(generado);
        }
    }
}
