using API.Application.DTO.departamento;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [ApiController]
    [Route("api/Departamento")]
    public class DepartamentoController : ControllerBase
    {
        private readonly IDepartamentoApplication _departamentoApplication;

        public DepartamentoController(IDepartamentoApplication departamentoApplication)
        {
            _departamentoApplication = departamentoApplication;
        }

        [HttpGet("{codigo}")]
        public async Task<ActionResult<DepartamentoDTO>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var departamento = await _departamentoApplication.ObtenerPorCodigoAsync(codigo);

            if (!departamento.Resultado)
                return BadRequest(new RespuestaError(departamento.Mensaje));

            if (departamento.Dato == null)
                return NotFound(new RespuestaError("Código de departamento no encontrado."));

            return Ok(departamento.Dato);
        }

        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<DepartamentoDTO>> ObtenerPorNombre([FromRoute] string nombre)
        {
            var departamento = await _departamentoApplication.ObtenerPorNombreAsync(nombre);

            if (!departamento.Resultado)
                return BadRequest(new RespuestaError(departamento.Mensaje));

            if (departamento.Dato == null)
                return NotFound(new RespuestaError("Nombre de departamento no encontrado."));

            return Ok(departamento.Dato);
        }

        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<List<DepartamentoDTO>>> ObtenerContengaNombre([FromRoute] string nombre)
        {
            var departamentos = await _departamentoApplication.ObtenerContengaNombreAsync(nombre);

            if (!departamentos.Resultado)
                return BadRequest(new RespuestaError(departamentos.Mensaje));

            return Ok(departamentos.Dato);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<List<DepartamentoDTO>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var departamentos = await _departamentoApplication.ObtenerContengaCodigoAsync(codigo);

            if (!departamentos.Resultado)
                return BadRequest(new RespuestaError(departamentos.Mensaje));

            return Ok(departamentos.Dato);
        }

        [HttpGet()]
        public async Task<ActionResult<List<DepartamentoDTO>>> ObtenerTodo()
        {
            var departamentos = await _departamentoApplication.ObtenerAsync();

            if (!departamentos.Resultado)
                return BadRequest(new RespuestaError(departamentos.Mensaje));

            return Ok(departamentos.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> Crear([FromBody] DepartamentoCrearDTO obj)
        {
            var insertar = await _departamentoApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(new RespuestaError(insertar.Mensaje));

            return Ok();
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult> Actualizar([FromRoute] string codigo, [FromBody] DepartamentoActualizarDTO obj)
        {
            var departamento = await _departamentoApplication.ObtenerPorCodigoAsync(codigo);

            if (departamento.Dato == null)
                return NotFound(new RespuestaError("Código de departamento no encontrado."));

            var actualizar = await _departamentoApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(new RespuestaError(actualizar.Mensaje));

            return Ok();
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult> Eliminar([FromRoute] string codigo)
        {
            var departamento = await _departamentoApplication.ObtenerPorCodigoAsync(codigo);

            if (departamento.Dato == null)
                return NotFound(new RespuestaError("Código de departamento no encontrado."));

            var eliminar = await _departamentoApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(new RespuestaError($"{eliminar.Mensaje}"));

            return Ok();
        }
    }
}
