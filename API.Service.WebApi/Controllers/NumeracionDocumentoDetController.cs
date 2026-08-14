using API.Application.DTO.numeracion.numeracion_documento_det;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [ApiController]
    [Route("api/NumeracionDocumentoDet")]
    public class NumeracionDocumentoDetController : ControllerBase
    {
        private readonly INumeracionDocumentoDetApplication _numeracionDocumentoDetApplication;

        public NumeracionDocumentoDetController(INumeracionDocumentoDetApplication numeracionDocumentoDetApplication)
        {
            _numeracionDocumentoDetApplication = numeracionDocumentoDetApplication;
        }

        [HttpGet("{codigoObj}")]
        public async Task<ActionResult<NumeracionDocumentoDetDTO>> Obtener([FromRoute] string codigoObj)
        {
            var numeracionDoc = await _numeracionDocumentoDetApplication.ObtenerAsync(codigoObj);

            if (!numeracionDoc.Resultado)
            {
                return BadRequest(new RespuestaError($"{numeracionDoc.Mensaje}"));
            }

            if (numeracionDoc.Dato == null)
            {
                return NotFound(new RespuestaError("El código del documento de numeración no se encontró."));
            }

            return Ok(numeracionDoc.Dato);
        }

        [HttpGet]
        public async Task<ActionResult<List<NumeracionDocumentoDetDTO>>> ObtenerTodoAsync()
        {
            var numeracionDocs = await _numeracionDocumentoDetApplication.ObtenerTodoAsync();

            if (!numeracionDocs.Resultado)
            {
                return BadRequest(new RespuestaError(numeracionDocs.Mensaje));
            }

            return Ok(numeracionDocs.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> InsertarAsync([FromBody] NumeracionDocumentoDetCrearDTO obj)
        {
            var insert = await _numeracionDocumentoDetApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(new RespuestaError(insert.Mensaje));
            }

            return Ok();
        }

        [HttpPut("{codigoObj}")]
        public async Task<ActionResult> ActualizarAsync([FromRoute] string codigoObj, [FromBody] NumeracionDocumentoDetActualizarDTO obj)
        {
            var numeracionDoc = await _numeracionDocumentoDetApplication.ObtenerAsync(codigoObj);

            if (numeracionDoc.Dato == null)
            {
                return NotFound(new RespuestaError("El código del documento de numeración no se encontró."));
            }

            var update = await _numeracionDocumentoDetApplication.ActualizarAsync(codigoObj, obj);

            if (!update.Resultado)
            {
                return BadRequest(new RespuestaError(update.Mensaje));
            }

            return Ok();
        }

        [HttpDelete("{codigoObj}")]
        public async Task<ActionResult> EliminarAsync([FromRoute] string codigoObj)
        {
            var numeracionDoc = await _numeracionDocumentoDetApplication.ObtenerAsync(codigoObj);

            if (numeracionDoc.Dato == null)
            {
                return NotFound(new RespuestaError("El código del documento de numeración no se encontró."));
            }

            var delete = await _numeracionDocumentoDetApplication.EliminarAsync(codigoObj);

            if (!delete.Resultado)
            {
                return BadRequest(new RespuestaError(delete.Mensaje));
            }

            return Ok();
        }
    }
}
