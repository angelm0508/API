using API.Application.DTO.socioNegocio;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [ApiController]
    [Route("api/SocioNegocio")]
    public class SocioNegocioController : ControllerBase
    {
        private readonly ISocioNegocioApplication _socioNegocioApplication;

        public SocioNegocioController(ISocioNegocioApplication socioNegocioApplication)
        {
            _socioNegocioApplication = socioNegocioApplication;
        }

        [HttpGet("{codigo}")]
        public async Task<ActionResult<SocioNegocioDTO>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var socioNegocio = await _socioNegocioApplication.ObtenerPorCodigoAsync(codigo);

            if (!socioNegocio.Resultado)
                return BadRequest(new RespuestaError(socioNegocio.Mensaje));

            if (socioNegocio.Dato == null)
                return NotFound(new RespuestaError("Código de socio negocio no encontrado."));

            return Ok(socioNegocio.Dato);
        }

        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<SocioNegocioDTO>> ObtenerPorNombre([FromRoute] string nombre)
        {
            var socioNegocio = await _socioNegocioApplication.ObtenerPorNombreAsync(nombre);

            if (!socioNegocio.Resultado)
                return BadRequest(new RespuestaError(socioNegocio.Mensaje));

            if (socioNegocio.Dato == null)
                return NotFound(new RespuestaError("Nombre de socio negocio no encontrado."));

            return Ok(socioNegocio.Dato);
        }

        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<List<SocioNegocioDTO>>> ObtenerContengaNombre([FromRoute] string nombre)
        {
            var sociosNegocios = await _socioNegocioApplication.ObtenerContengaNombreAsync(nombre);

            if (!sociosNegocios.Resultado)
                return BadRequest(new RespuestaError(sociosNegocios.Mensaje));

            return Ok(sociosNegocios.Dato);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<List<SocioNegocioDTO>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var sociosNegocios = await _socioNegocioApplication.ObtenerContengaCodigoAsync(codigo);

            if (!sociosNegocios.Resultado)
                return BadRequest(new RespuestaError(sociosNegocios.Mensaje));

            return Ok(sociosNegocios.Dato);
        }

        [HttpGet()]
        public async Task<ActionResult<List<SocioNegocioDTO>>> ObtenerTodo()
        {
            var sociosNegocios = await _socioNegocioApplication.ObtenerAsync();

            if (!sociosNegocios.Resultado)
                return BadRequest(new RespuestaError(sociosNegocios.Mensaje));

            return Ok(sociosNegocios.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> Crear([FromBody] SocioNegocioCrearDTO obj)
        {
            var insertar = await _socioNegocioApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(new RespuestaError(insertar.Mensaje));

            return Ok();
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult> Actualizar([FromRoute] string codigo, [FromBody] SocioNegocioActualizarDTO obj)
        {
            var socioNegocio = await _socioNegocioApplication.ObtenerPorCodigoAsync(codigo);

            if (socioNegocio.Dato == null)
                return NotFound(new RespuestaError("Código de socio negocio no encontrado."));

            var actualizar = await _socioNegocioApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(new RespuestaError(actualizar.Mensaje));

            return Ok();
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult> Eliminar([FromRoute] string codigo)
        {
            var socioNegocio = await _socioNegocioApplication.ObtenerPorCodigoAsync(codigo);

            if (socioNegocio.Dato == null)
                return NotFound(new RespuestaError("Código de socio negocio no encontrado."));

            var eliminar = await _socioNegocioApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(new RespuestaError($"{eliminar.Mensaje}"));

            return Ok();
        }
    }
}
