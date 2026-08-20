using API.Application.DTO.municipio;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Municipio")]
    public class MunicipioController : ControllerBase
    {
        private readonly IMunicipioApplication _municipioApplication;

        public MunicipioController(IMunicipioApplication municipioApplication)
        {
            _municipioApplication = municipioApplication;
        }

        [HttpGet("{codigo}")]
        public async Task<ActionResult<MunicipioDTO>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var municipio = await _municipioApplication.ObtenerPorCodigoAsync(codigo);

            if (!municipio.Resultado)
                return BadRequest(new RespuestaError(municipio.Mensaje));

            if (municipio.Dato == null)
                return NotFound(new RespuestaError("Código de municipio no encontrado."));

            return Ok(municipio.Dato);
        }

        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<MunicipioDTO>> ObtenerPorNombre([FromRoute] string nombre)
        {
            var municipio = await _municipioApplication.ObtenerPorNombreAsync(nombre);

            if (!municipio.Resultado)
                return BadRequest(new RespuestaError(municipio.Mensaje));

            if (municipio.Dato == null)
                return NotFound(new RespuestaError("Nombre de municipio no encontrado."));

            return Ok(municipio.Dato);
        }

        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<List<MunicipioDTO>>> ObtenerContengaNombre([FromRoute] string nombre)
        {
            var municipios = await _municipioApplication.ObtenerContengaNombreAsync(nombre);

            if (!municipios.Resultado)
                return BadRequest(new RespuestaError(municipios.Mensaje));

            return Ok(municipios.Dato);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<List<MunicipioDTO>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var municipios = await _municipioApplication.ObtenerContengaCodigoAsync(codigo);

            if (!municipios.Resultado)
                return BadRequest(new RespuestaError(municipios.Mensaje));

            return Ok(municipios.Dato);
        }

        [HttpGet()]
        public async Task<ActionResult<List<MunicipioDTO>>> ObtenerTodo()
        {
            var municipios = await _municipioApplication.ObtenerAsync();

            if (!municipios.Resultado)
                return BadRequest(new RespuestaError(municipios.Mensaje));

            return Ok(municipios.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> Crear([FromBody] MunicipioCrearDTO obj)
        {
            var insertar = await _municipioApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(new RespuestaError(insertar.Mensaje));

            return Ok();
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult> Actualizar([FromRoute] string codigo, [FromBody] MunicipioActualizarDTO obj)
        {
            var municipio = await _municipioApplication.ObtenerPorCodigoAsync(codigo);

            if (municipio.Dato == null)
                return NotFound(new RespuestaError("Código de municipio no encontrado."));

            var actualizar = await _municipioApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(new RespuestaError(actualizar.Mensaje));

            return Ok();
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult> Eliminar([FromRoute] string codigo)
        {
            var municipio = await _municipioApplication.ObtenerPorCodigoAsync(codigo);

            if (municipio.Dato == null)
                return NotFound(new RespuestaError("Código de municipio no encontrado."));

            var eliminar = await _municipioApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(new RespuestaError($"{eliminar.Mensaje}"));

            return Ok();
        }
    }
}
