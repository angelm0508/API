using API.Application.DTO;
using API.Application.DTO.municipio;
using API.Application.Interface;
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
        public async Task<ActionResult<Respuesta<MunicipioDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var municipio = await _municipioApplication.ObtenerPorCodigoAsync(codigo);

            if (!municipio.Resultado)
                return BadRequest(municipio);

            if (municipio.Dato == null)
            {
                municipio.Resultado = false;
                municipio.Mensaje = "Código de municipio no encontrado.";
                return NotFound(municipio);
            }

            return Ok(municipio);
        }

        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<Respuesta<MunicipioDTO>>> ObtenerPorNombre([FromRoute] string nombre)
        {
            var municipio = await _municipioApplication.ObtenerPorNombreAsync(nombre);

            if (!municipio.Resultado)
                return BadRequest(municipio);

            if (municipio.Dato == null)
            {
                municipio.Resultado = false;
                municipio.Mensaje = "Nombre de municipio no encontrado.";
                return NotFound(municipio);
            }

            return Ok(municipio);
        }

        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<Respuesta<IEnumerable<MunicipioDTO>>>> ObtenerContengaNombre([FromRoute] string nombre)
        {
            var municipios = await _municipioApplication.ObtenerContengaNombreAsync(nombre);

            if (!municipios.Resultado)
                return BadRequest(municipios);

            return Ok(municipios);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<MunicipioDTO>>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var municipios = await _municipioApplication.ObtenerContengaCodigoAsync(codigo);

            if (!municipios.Resultado)
                return BadRequest(municipios);

            return Ok(municipios);
        }

        [HttpGet()]
        public async Task<ActionResult<Respuesta<IEnumerable<MunicipioDTO>>>> ObtenerTodo()
        {
            var municipios = await _municipioApplication.ObtenerAsync();

            if (!municipios.Resultado)
                return BadRequest(municipios);

            return Ok(municipios);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] MunicipioCrearDTO obj)
        {
            var insertar = await _municipioApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] MunicipioActualizarDTO obj)
        {
            var municipio = await _municipioApplication.ObtenerPorCodigoAsync(codigo);

            if (municipio.Dato == null)
            {
                municipio.Resultado = false;
                municipio.Mensaje = "Código de municipio no encontrado.";
                return NotFound(municipio);
            }

            var actualizar = await _municipioApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var municipio = await _municipioApplication.ObtenerPorCodigoAsync(codigo);

            if (municipio.Dato == null)
            {
                municipio.Resultado = false;
                municipio.Mensaje = "Código de municipio no encontrado.";
                return NotFound(municipio);
            }

            var eliminar = await _municipioApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
