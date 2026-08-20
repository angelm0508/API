using API.Application.DTO;
using API.Application.DTO.pais;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
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
        public async Task<ActionResult<Respuesta<PaisDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var pais = await _paisApplication.ObtenerPorCodigoAsync(codigo);

            if (!pais.Resultado)
                return BadRequest(pais);

            if (pais.Dato == null)
            {
                pais.Resultado = false;
                pais.Mensaje = "Código de país no encontrado.";
                return NotFound(pais);
            }

            return Ok(pais);
        }

        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<Respuesta<PaisDTO>>> ObtenerPorNombre([FromRoute] string nombre)
        {
            var pais = await _paisApplication.ObtenerPorNombreAsync(nombre);

            if (!pais.Resultado)
                return BadRequest(pais);

            if (pais.Dato == null)
            {
                pais.Resultado = false;
                pais.Mensaje = "Nombre de país no encontrado.";
                return NotFound(pais);
            }

            return Ok(pais);
        }

        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<Respuesta<IEnumerable<PaisDTO>>>> ObtenerContengaNombre([FromRoute] string nombre)
        {
            var paises = await _paisApplication.ObtenerContengaNombreAsync(nombre);

            if (!paises.Resultado)
                return BadRequest(paises);

            return Ok(paises);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<PaisDTO>>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var paises = await _paisApplication.ObtenerContengaCodigoAsync(codigo);

            if (!paises.Resultado)
                return BadRequest(paises);

            return Ok(paises);
        }

        [HttpGet()]
        public async Task<ActionResult<Respuesta<IEnumerable<PaisDTO>>>> ObtenerTodo()
        {
            var paises = await _paisApplication.ObtenerAsync();

            if (!paises.Resultado)
                return BadRequest(paises);

            return Ok(paises);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] PaisCrearDTO obj)
        {
            var insertar = await _paisApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] PaisActualizarDTO obj)
        {
            var pais = await _paisApplication.ObtenerPorCodigoAsync(codigo);

            if (pais.Dato == null)
            {
                pais.Resultado = false;
                pais.Mensaje = "Código de país no encontrado.";
                return NotFound(pais);
            }

            var actualizar = await _paisApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var pais = await _paisApplication.ObtenerPorCodigoAsync(codigo);

            if (pais.Dato == null)
            {
                pais.Resultado = false;
                pais.Mensaje = "Código de país no encontrado.";
                return NotFound(pais);
            }

            var eliminar = await _paisApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
