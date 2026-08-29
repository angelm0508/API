using API.Application.DTO;
using API.Application.DTO.impuesto;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Impuesto")]
    public class ImpuestoController : ControllerBase
    {
        private readonly IImpuestoApplication _impuestoApplication;

        public ImpuestoController(IImpuestoApplication impuestoApplication)
        {
            _impuestoApplication = impuestoApplication;
        }

        [HttpGet("{codigo}")]
        public async Task<ActionResult<Respuesta<ImpuestoDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var impuesto = await _impuestoApplication.ObtenerPorCodigoAsync(codigo);

            if (!impuesto.Resultado)
                return BadRequest(impuesto);

            if (impuesto.Dato == null)
            {
                impuesto.Resultado = false;
                impuesto.Mensaje = "Código de impuesto no encontrado.";
                return NotFound(impuesto);
            }

            return Ok(impuesto);
        }

        [HttpGet()]
        public async Task<ActionResult<Respuesta<IEnumerable<ImpuestoDTO>>>> ObtenerTodo()
        {
            var impuestos = await _impuestoApplication.ObtenerAsync();

            if (!impuestos.Resultado)
                return BadRequest(impuestos);

            return Ok(impuestos);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] ImpuestoCrearDTO obj)
        {
            var insertar = await _impuestoApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] ImpuestoActualizarDTO obj)
        {
            var impuesto = await _impuestoApplication.ObtenerPorCodigoAsync(codigo);

            if (impuesto.Dato == null)
            {
                impuesto.Resultado = false;
                impuesto.Mensaje = "Código de impuesto no encontrado.";
                return NotFound(impuesto);
            }

            var actualizar = await _impuestoApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var impuesto = await _impuestoApplication.ObtenerPorCodigoAsync(codigo);

            if (impuesto.Dato == null)
            {
                impuesto.Resultado = false;
                impuesto.Mensaje = "Código de impuesto no encontrado.";
                return NotFound(impuesto);
            }

            var eliminar = await _impuestoApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
