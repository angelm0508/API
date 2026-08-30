using API.Application.DTO;
using API.Application.DTO.socioNegocio;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
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
        public async Task<ActionResult<Respuesta<SocioNegocioDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var socioNegocio = await _socioNegocioApplication.ObtenerPorCodigoAsync(codigo);

            if (!socioNegocio.Resultado)
                return BadRequest(socioNegocio);

            if (socioNegocio.Dato == null)
            {
                socioNegocio.Resultado = false;
                socioNegocio.Mensaje = "Código de socio negocio no encontrado.";
                return NotFound(socioNegocio);
            }

            return Ok(socioNegocio);
        }

        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<Respuesta<SocioNegocioDTO>>> ObtenerPorNombre([FromRoute] string nombre)
        {
            var socioNegocio = await _socioNegocioApplication.ObtenerPorNombreAsync(nombre);

            if (!socioNegocio.Resultado)
                return BadRequest(socioNegocio);

            if (socioNegocio.Dato == null)
            {
                socioNegocio.Resultado = false;
                socioNegocio.Mensaje = "Nombre de socio negocio no encontrado.";
                return NotFound(socioNegocio);
            }

            return Ok(socioNegocio);
        }

        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<Respuesta<IEnumerable<SocioNegocioDTO>>>> ObtenerContengaNombre([FromRoute] string nombre, [FromQuery] string? tipo)
        {
            var sociosNegocios = await _socioNegocioApplication.ObtenerContengaNombreAsync(nombre, tipo);

            if (!sociosNegocios.Resultado)
                return BadRequest(sociosNegocios);

            return Ok(sociosNegocios);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<SocioNegocioDTO>>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var sociosNegocios = await _socioNegocioApplication.ObtenerContengaCodigoAsync(codigo);

            if (!sociosNegocios.Resultado)
                return BadRequest(sociosNegocios);

            return Ok(sociosNegocios);
        }

        [HttpGet()]
        public async Task<ActionResult<Respuesta<IEnumerable<SocioNegocioDTO>>>> ObtenerTodo([FromQuery] string? tipo)
        {
            var sociosNegocios = await _socioNegocioApplication.ObtenerAsync(tipo);

            if (!sociosNegocios.Resultado)
                return BadRequest(sociosNegocios);

            return Ok(sociosNegocios);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<string>>> Crear([FromBody] SocioNegocioCrearDTO obj)
        {
            var insertar = await _socioNegocioApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] SocioNegocioActualizarDTO obj)
        {
            var socioNegocio = await _socioNegocioApplication.ObtenerPorCodigoAsync(codigo);

            if (socioNegocio.Dato == null)
            {
                socioNegocio.Resultado = false;
                socioNegocio.Mensaje = "Código de socio negocio no encontrado.";
                return NotFound(socioNegocio);
            }

            var actualizar = await _socioNegocioApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var socioNegocio = await _socioNegocioApplication.ObtenerPorCodigoAsync(codigo);

            if (socioNegocio.Dato == null)
            {
                socioNegocio.Resultado = false;
                socioNegocio.Mensaje = "Código de socio negocio no encontrado.";
                return NotFound(socioNegocio);
            }

            var eliminar = await _socioNegocioApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
