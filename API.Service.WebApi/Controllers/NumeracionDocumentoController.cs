using API.Application.DTO;
using API.Application.DTO.numeracionDocumento;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
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

        [RequierePermiso("NumeracionDocumento", Accion.Ver)]
        [HttpGet("{codigo}")]
        public async Task<ActionResult<Respuesta<NumeracionDocumentoDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var numeracion = await _numeracionApplication.ObtenerPorCodigoAsync(codigo);

            if (!numeracion.Resultado)
                return BadRequest(numeracion);

            if (numeracion.Dato == null)
            {
                numeracion.Resultado = false;
                numeracion.Mensaje = "Código de numeración no encontrado.";
                return NotFound(numeracion);
            }

            return Ok(numeracion);
        }

        [RequierePermiso("NumeracionDocumento", Accion.Ver)]
        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<NumeracionDocumentoDTO>>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var numeraciones = await _numeracionApplication.ObtenerContengaCodigoAsync(codigo);

            if (!numeraciones.Resultado)
                return BadRequest(numeraciones);

            return Ok(numeraciones);
        }

        [RequierePermiso("NumeracionDocumento", Accion.Ver)]
        [HttpGet()]
        public async Task<ActionResult<Respuesta<IEnumerable<NumeracionDocumentoDTO>>>> ObtenerTodo()
        {
            var numeraciones = await _numeracionApplication.ObtenerAsync();

            if (!numeraciones.Resultado)
                return BadRequest(numeraciones);

            return Ok(numeraciones);
        }

        [RequierePermiso("NumeracionDocumento", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] NumeracionDocumentoCrearDTO obj)
        {
            var insertar = await _numeracionApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("NumeracionDocumento", Accion.Actualizar)]
        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] NumeracionDocumentoActualizarDTO obj)
        {
            var numeracion = await _numeracionApplication.ObtenerPorCodigoAsync(codigo);

            if (numeracion.Dato == null)
            {
                numeracion.Resultado = false;
                numeracion.Mensaje = "Código de numeración no encontrado.";
                return NotFound(numeracion);
            }

            var actualizar = await _numeracionApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("NumeracionDocumento", Accion.Eliminar)]
        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var numeracion = await _numeracionApplication.ObtenerPorCodigoAsync(codigo);

            if (numeracion.Dato == null)
            {
                numeracion.Resultado = false;
                numeracion.Mensaje = "Código de numeración no encontrado.";
                return NotFound(numeracion);
            }

            var eliminar = await _numeracionApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
