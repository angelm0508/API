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

        [HttpGet("{codigoObj}")]
        public async Task<ActionResult<Respuesta<NumeracionDocumentoDetDTO>>> Obtener([FromRoute] string codigoObj)
        {
            var numeracionDoc = await _numeracionDocumentoDetApplication.ObtenerAsync(codigoObj);

            if (!numeracionDoc.Resultado)
            {
                return BadRequest(numeracionDoc);
            }

            if (numeracionDoc.Dato == null)
            {
                numeracionDoc.Resultado = false;
                numeracionDoc.Mensaje = "El código del documento de numeración no se encontró.";
                return NotFound(numeracionDoc);
            }

            return Ok(numeracionDoc);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<NumeracionDocumentoDetDTO>>>> ObtenerTodoAsync()
        {
            var numeracionDocs = await _numeracionDocumentoDetApplication.ObtenerTodoAsync();

            if (!numeracionDocs.Resultado)
            {
                return BadRequest(numeracionDocs);
            }

            return Ok(numeracionDocs);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<string>>> InsertarAsync([FromBody] NumeracionDocumentoDetCrearDTO obj)
        {
            var insert = await _numeracionDocumentoDetApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{codigoObj}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] string codigoObj, [FromBody] NumeracionDocumentoDetActualizarDTO obj)
        {
            var numeracionDoc = await _numeracionDocumentoDetApplication.ObtenerAsync(codigoObj);

            if (numeracionDoc.Dato == null)
            {
                numeracionDoc.Resultado = false;
                numeracionDoc.Mensaje = "El código del documento de numeración no se encontró.";
                return NotFound(numeracionDoc);
            }

            var update = await _numeracionDocumentoDetApplication.ActualizarAsync(codigoObj, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{codigoObj}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] string codigoObj)
        {
            var numeracionDoc = await _numeracionDocumentoDetApplication.ObtenerAsync(codigoObj);

            if (numeracionDoc.Dato == null)
            {
                numeracionDoc.Resultado = false;
                numeracionDoc.Mensaje = "El código del documento de numeración no se encontró.";
                return NotFound(numeracionDoc);
            }

            var delete = await _numeracionDocumentoDetApplication.EliminarAsync(codigoObj);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
