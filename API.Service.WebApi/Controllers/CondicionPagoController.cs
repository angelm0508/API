using API.Application.DTO;
using API.Application.DTO.condicionPago;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/CondicionPago")]
    public class CondicionPagoController : ControllerBase
    {
        private const string NoEncontrada = "Código de condición de pago no encontrado.";

        private readonly ICondicionPagoApplication _application;

        public CondicionPagoController(ICondicionPagoApplication application)
        {
            _application = application;
        }

        [RequierePermiso("CondicionesPago", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<CondicionPagoDTO>>>> ObtenerTodo()
        {
            var condiciones = await _application.ObtenerAsync();

            if (!condiciones.Resultado)
                return BadRequest(condiciones);

            return Ok(condiciones);
        }

        [RequierePermiso("CondicionesPago", Accion.Ver)]
        [HttpGet("{codigo}")]
        public async Task<ActionResult<Respuesta<CondicionPagoDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var condicion = await _application.ObtenerPorCodigoAsync(codigo);

            if (!condicion.Resultado)
                return BadRequest(condicion);

            if (condicion.Dato == null)
            {
                condicion.Resultado = false;
                condicion.Mensaje = NoEncontrada;
                return NotFound(condicion);
            }

            return Ok(condicion);
        }

        [RequierePermiso("CondicionesPago", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] CondicionPagoCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("CondicionesPago", Accion.Actualizar)]
        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] CondicionPagoActualizarDTO obj)
        {
            var condicion = await _application.ObtenerPorCodigoAsync(codigo);

            if (condicion.Dato == null)
            {
                condicion.Resultado = false;
                condicion.Mensaje = NoEncontrada;
                return NotFound(condicion);
            }

            var actualizar = await _application.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("CondicionesPago", Accion.Eliminar)]
        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var condicion = await _application.ObtenerPorCodigoAsync(codigo);

            if (condicion.Dato == null)
            {
                condicion.Resultado = false;
                condicion.Mensaje = NoEncontrada;
                return NotFound(condicion);
            }

            var eliminar = await _application.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
