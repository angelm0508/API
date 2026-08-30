using API.Application.DTO;
using API.Application.DTO.departamento;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
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
        public async Task<ActionResult<Respuesta<DepartamentoDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var departamento = await _departamentoApplication.ObtenerPorCodigoAsync(codigo);

            if (!departamento.Resultado)
                return BadRequest(departamento);

            if (departamento.Dato == null)
            {
                departamento.Resultado = false;
                departamento.Mensaje = "Código de departamento no encontrado.";
                return NotFound(departamento);
            }

            return Ok(departamento);
        }

        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<Respuesta<DepartamentoDTO>>> ObtenerPorNombre([FromRoute] string nombre)
        {
            var departamento = await _departamentoApplication.ObtenerPorNombreAsync(nombre);

            if (!departamento.Resultado)
                return BadRequest(departamento);

            if (departamento.Dato == null)
            {
                departamento.Resultado = false;
                departamento.Mensaje = "Nombre de departamento no encontrado.";
                return NotFound(departamento);
            }

            return Ok(departamento);
        }

        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<Respuesta<IEnumerable<DepartamentoDTO>>>> ObtenerContengaNombre([FromRoute] string nombre)
        {
            var departamentos = await _departamentoApplication.ObtenerContengaNombreAsync(nombre);

            if (!departamentos.Resultado)
                return BadRequest(departamentos);

            return Ok(departamentos);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<DepartamentoDTO>>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var departamentos = await _departamentoApplication.ObtenerContengaCodigoAsync(codigo);

            if (!departamentos.Resultado)
                return BadRequest(departamentos);

            return Ok(departamentos);
        }

        [HttpGet()]
        public async Task<ActionResult<Respuesta<IEnumerable<DepartamentoDTO>>>> ObtenerTodo()
        {
            var departamentos = await _departamentoApplication.ObtenerAsync();

            if (!departamentos.Resultado)
                return BadRequest(departamentos);

            return Ok(departamentos);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] DepartamentoCrearDTO obj)
        {
            var insertar = await _departamentoApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] DepartamentoActualizarDTO obj)
        {
            var departamento = await _departamentoApplication.ObtenerPorCodigoAsync(codigo);

            if (departamento.Dato == null)
            {
                departamento.Resultado = false;
                departamento.Mensaje = "Código de departamento no encontrado.";
                return NotFound(departamento);
            }

            var actualizar = await _departamentoApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var departamento = await _departamentoApplication.ObtenerPorCodigoAsync(codigo);

            if (departamento.Dato == null)
            {
                departamento.Resultado = false;
                departamento.Mensaje = "Código de departamento no encontrado.";
                return NotFound(departamento);
            }

            var eliminar = await _departamentoApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
