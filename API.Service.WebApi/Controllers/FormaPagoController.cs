using API.Application.DTO;
using API.Application.DTO.formaPago;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/FormaPago")]
    public class FormaPagoController : ControllerBase
    {
        private const string NoEncontrada = "Código de forma de pago no encontrado.";

        private readonly IFormaPagoApplication _application;

        public FormaPagoController(IFormaPagoApplication application)
        {
            _application = application;
        }

        [RequierePermiso("FormasPago", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<FormaPagoDTO>>>> ObtenerTodo()
        {
            var formas = await _application.ObtenerAsync();

            if (!formas.Resultado)
                return BadRequest(formas);

            return Ok(formas);
        }

        [RequierePermiso("FormasPago", Accion.Ver)]
        [HttpGet("{codigo}")]
        public async Task<ActionResult<Respuesta<FormaPagoDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var forma = await _application.ObtenerPorCodigoAsync(codigo);

            if (!forma.Resultado)
                return BadRequest(forma);

            if (forma.Dato == null)
            {
                forma.Resultado = false;
                forma.Mensaje = NoEncontrada;
                return NotFound(forma);
            }

            return Ok(forma);
        }

        [RequierePermiso("FormasPago", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] FormaPagoCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("FormasPago", Accion.Actualizar)]
        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] FormaPagoActualizarDTO obj)
        {
            var forma = await _application.ObtenerPorCodigoAsync(codigo);

            if (forma.Dato == null)
            {
                forma.Resultado = false;
                forma.Mensaje = NoEncontrada;
                return NotFound(forma);
            }

            var actualizar = await _application.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("FormasPago", Accion.Eliminar)]
        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var forma = await _application.ObtenerPorCodigoAsync(codigo);

            if (forma.Dato == null)
            {
                forma.Resultado = false;
                forma.Mensaje = NoEncontrada;
                return NotFound(forma);
            }

            var eliminar = await _application.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
