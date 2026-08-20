using API.Application.DTO;
using API.Application.DTO.moneda;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Moneda")]
    public class MonedaController : ControllerBase
    {
        private readonly IMonedaApplication _monedaApplication;

        public MonedaController(IMonedaApplication monedaApplication)
        {
            _monedaApplication = monedaApplication;
        }

        [HttpGet("{codigo}")]
        public async Task<ActionResult<Respuesta<MonedaDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var moneda = await _monedaApplication.ObtenerPorCodigoAsync(codigo);

            if (!moneda.Resultado)
                return BadRequest(moneda);

            if (moneda.Dato == null)
            {
                moneda.Resultado = false;
                moneda.Mensaje = "Código de moneda no encontrado.";
                return NotFound(moneda);
            }

            return Ok(moneda);
        }

        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<Respuesta<MonedaDTO>>> ObtenerPorNombre([FromRoute] string nombre)
        {
            var moneda = await _monedaApplication.ObtenerPorNombreAsync(nombre);

            if (!moneda.Resultado)
                return BadRequest(moneda);

            if (moneda.Dato == null)
            {
                moneda.Resultado = false;
                moneda.Mensaje = "Nombre de moneda no encontrado.";
                return NotFound(moneda);
            }

            return Ok(moneda);
        }

        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<Respuesta<IEnumerable<MonedaDTO>>>> ObtenerContengaNombre([FromRoute] string nombre)
        {
            var monedas = await _monedaApplication.ObtenerContengaNombreAsync(nombre);

            if (!monedas.Resultado)
                return BadRequest(monedas);

            return Ok(monedas);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<MonedaDTO>>>> ObtenerContengaCodigo([FromRoute] string codigo)
        {
            var monedas = await _monedaApplication.ObtenerContengaCodigoAsync(codigo);

            if (!monedas.Resultado)
                return BadRequest(monedas);

            return Ok(monedas);
        }

        [HttpGet()]
        public async Task<ActionResult<Respuesta<IEnumerable<MonedaDTO>>>> ObtenerTodo()
        {
            var monedas = await _monedaApplication.ObtenerAsync();

            if (!monedas.Resultado)
                return BadRequest(monedas);

            return Ok(monedas);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] MonedaCrearDTO obj)
        {
            var insertar = await _monedaApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] MonedaActualizarDTO obj)
        {
            var moneda = await _monedaApplication.ObtenerPorCodigoAsync(codigo);

            if (moneda.Dato == null)
            {
                moneda.Resultado = false;
                moneda.Mensaje = "Código de moneda no encontrado.";
                return NotFound(moneda);
            }

            var actualizar = await _monedaApplication.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var moneda = await _monedaApplication.ObtenerPorCodigoAsync(codigo);

            if (moneda.Dato == null)
            {
                moneda.Resultado = false;
                moneda.Mensaje = "Código de moneda no encontrado.";
                return NotFound(moneda);
            }

            var eliminar = await _monedaApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
