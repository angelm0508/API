using API.Application.DTO.numeracionDocumento;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/NumeracionDocumento")]
    public class NumeracionDocumentoController : ControllerBase
    {
        private readonly INumeracionDocumentoApplication _numeracionApplication;

        public NumeracionDocumentoController(INumeracionDocumentoApplication numeracionApplication)
        {
            _numeracionApplication = numeracionApplication;
        }

        [HttpGet("{codigo}")]
        public async Task<ActionResult<NumeracionDocumentoDTO>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var numeracion = await _numeracionApplication.ObtenerPorCodigoAsync(codigo);

            if (!numeracion.Resultado)
                return BadRequest(new RespuestaError(numeracion.Mensaje));

            if (numeracion.Dato == null)
                return NotFound(new RespuestaError("Código de numeración no encontrado."));

            return Ok(numeracion.Dato);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<List<NumeracionDocumentoDTO>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var numeraciones = await _numeracionApplication.ObtenerContengaCodigoAsync(codigo);

            if (!numeraciones.Resultado)
                return BadRequest(new RespuestaError(numeraciones.Mensaje));

            return Ok(numeraciones.Dato);
        }

        [HttpGet()]
        public async Task<ActionResult<List<NumeracionDocumentoDTO>>> ObtenerTodo()
        {
            var numeraciones = await _numeracionApplication.ObtenerAsync();

            if (!numeraciones.Resultado)
                return BadRequest(new RespuestaError(numeraciones.Mensaje));

            return Ok(numeraciones.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> Crear([FromBody] NumeracionDocumentoCrearDTO obj)
        {
            var insertar = await _numeracionApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(new RespuestaError(insertar.Mensaje));

            return Ok();
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult> Actualizar([FromRoute] string codigo, [FromBody] NumeracionDocumentoActualizarDTO obj)
        {
            var numeracion = await _numeracionApplication.ObtenerPorCodigoAsync(codigo);

            if (numeracion.Dato == null)
                return NotFound(new RespuestaError("Código de numeración no encontrado."));

            var actualizar = await _numeracionApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(new RespuestaError(actualizar.Mensaje));

            return Ok();
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult> Eliminar([FromRoute] string codigo)
        {
            var numeracion = await _numeracionApplication.ObtenerPorCodigoAsync(codigo);

            if (numeracion.Dato == null)
                return NotFound(new RespuestaError("Código de numeración no encontrado."));

            var eliminar = await _numeracionApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(new RespuestaError($"{eliminar.Mensaje}"));

            return Ok();
        }
    }
}
