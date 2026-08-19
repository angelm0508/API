using API.Application.DTO.direccionSocioNegocio;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [ApiController]
    [Route("api/DireccionSocioNegocio")]
    public class DireccionSocioNegocioController : ControllerBase
    {
        private readonly IDireccionSocioNegocioApplication _direccionApplication;

        public DireccionSocioNegocioController(IDireccionSocioNegocioApplication direccionApplication)
        {
            _direccionApplication = direccionApplication;
        }

        [HttpGet("{codigo}")]
        public async Task<ActionResult<DireccionSocioNegocioDTO>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var direccion = await _direccionApplication.ObtenerPorCodigoAsync(codigo);

            if (!direccion.Resultado)
                return BadRequest(new RespuestaError(direccion.Mensaje));

            if (direccion.Dato == null)
                return NotFound(new RespuestaError("Código de dirección no encontrado."));

            return Ok(direccion.Dato);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<List<DireccionSocioNegocioDTO>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var direcciones = await _direccionApplication.ObtenerContengaCodigoAsync(codigo);

            if (!direcciones.Resultado)
                return BadRequest(new RespuestaError(direcciones.Mensaje));

            return Ok(direcciones.Dato);
        }

        [HttpGet()]
        public async Task<ActionResult<List<DireccionSocioNegocioDTO>>> ObtenerTodo()
        {
            var direcciones = await _direccionApplication.ObtenerAsync();

            if (!direcciones.Resultado)
                return BadRequest(new RespuestaError(direcciones.Mensaje));

            return Ok(direcciones.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> Crear([FromBody] DireccionSocioNegocioCrearDTO obj)
        {
            var insertar = await _direccionApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(new RespuestaError(insertar.Mensaje));

            return Ok();
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult> Actualizar([FromRoute] string codigo, [FromBody] DireccionSocioNegocioActualizarDTO obj)
        {
            var direccion = await _direccionApplication.ObtenerPorCodigoAsync(codigo);

            if (direccion.Dato == null)
                return NotFound(new RespuestaError("Código de dirección no encontrado."));

            var actualizar = await _direccionApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(new RespuestaError(actualizar.Mensaje));

            return Ok();
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult> Eliminar([FromRoute] string codigo)
        {
            var direccion = await _direccionApplication.ObtenerPorCodigoAsync(codigo);

            if (direccion.Dato == null)
                return NotFound(new RespuestaError("Código de dirección no encontrado."));

            var eliminar = await _direccionApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(new RespuestaError($"{eliminar.Mensaje}"));

            return Ok();
        }
    }
}
