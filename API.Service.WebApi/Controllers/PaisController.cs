using API.Application.DTO.pais;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [ApiController]
    [Route("api/Pais")]
    public class PaisController : ControllerBase
    {
        private readonly IPaisApplication _paisApplication;

        public PaisController(IPaisApplication paisApplication)
        {
            _paisApplication = paisApplication;
        }

        [HttpGet("{codigo}")]
        public async Task<ActionResult<PaisDTO>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var pais = await _paisApplication.ObtenerPorCodigoAsync(codigo);

            if (!pais.Resultado)
                return BadRequest(new RespuestaError(pais.Mensaje));

            if (pais.Dato == null)
                return NotFound(new RespuestaError("Código de país no encontrado."));

            return Ok(pais.Dato);
        }

        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<PaisDTO>> ObtenerPorNombre([FromRoute] string nombre)
        {
            var pais = await _paisApplication.ObtenerPorNombreAsync(nombre);

            if (!pais.Resultado)
                return BadRequest(new RespuestaError(pais.Mensaje));

            if (pais.Dato == null)
                return NotFound(new RespuestaError("Nombre de país no encontrado."));

            return Ok(pais.Dato);
        }

        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<List<PaisDTO>>> ObtenerContengaNombre([FromRoute] string nombre)
        {
            var paises = await _paisApplication.ObtenerContengaNombreAsync(nombre);

            if (!paises.Resultado)
                return BadRequest(new RespuestaError(paises.Mensaje));

            return Ok(paises.Dato);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<List<PaisDTO>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var paises = await _paisApplication.ObtenerContengaCodigoAsync(codigo);

            if (!paises.Resultado)
                return BadRequest(new RespuestaError(paises.Mensaje));

            return Ok(paises.Dato);
        }

        [HttpGet()]
        public async Task<ActionResult<List<PaisDTO>>> ObtenerTodo()
        {
            var paises = await _paisApplication.ObtenerAsync();

            if (!paises.Resultado)
                return BadRequest(new RespuestaError(paises.Mensaje));

            return Ok(paises.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> Crear([FromBody] PaisCrearDTO obj)
        {
            var insertar = await _paisApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(new RespuestaError(insertar.Mensaje));

            return Ok();
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult> Actualizar([FromRoute] string codigo, [FromBody] PaisActualizarDTO obj)
        {
            var pais = await _paisApplication.ObtenerPorCodigoAsync(codigo);

            if (pais.Dato == null)
                return NotFound(new RespuestaError("Código de país no encontrado."));

            var actualizar = await _paisApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(new RespuestaError(actualizar.Mensaje));

            return Ok();
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult> Eliminar([FromRoute] string codigo)
        {
            var pais = await _paisApplication.ObtenerPorCodigoAsync(codigo);

            if (pais.Dato == null)
                return NotFound(new RespuestaError("Código de país no encontrado."));

            var eliminar = await _paisApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(new RespuestaError($"{eliminar.Mensaje}"));

            return Ok();
        }
    }
}
