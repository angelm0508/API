using API.Application.DTO;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/OrdenServicio")]
    public class OrdenServicioController : ControllerBase
    {
        private const string NoEncontrada = "La orden de servicio no se encontró.";

        private readonly IOrdenServicioApplication _application;

        public OrdenServicioController(IOrdenServicioApplication application)
        {
            _application = application;
        }

        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<OrdenServicioDTO>>> Obtener([FromRoute] int id)
        {
            var orden = await _application.ObtenerAsync(id);

            if (!orden.Resultado)
                return BadRequest(orden);

            if (orden.Dato == null)
            {
                orden.Resultado = false;
                orden.Mensaje = NoEncontrada;
                return NotFound(orden);
            }

            return Ok(orden);
        }

        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<OrdenServicioDTO>>>> ObtenerTodoAsync([FromQuery] string? estado)
        {
            var ordenes = await _application.ObtenerTodoAsync(estado);

            if (!ordenes.Resultado)
                return BadRequest(ordenes);

            return Ok(ordenes);
        }

        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        [HttpGet("{id:int}/Lineas")]
        public async Task<ActionResult<Respuesta<IEnumerable<OrdenServicioDetalleDTO>>>> ObtenerLineas([FromRoute] int id)
        {
            var lineas = await _application.ObtenerLineasAsync(id);

            if (!lineas.Resultado)
                return BadRequest(lineas);

            return Ok(lineas);
        }

        [RequierePermiso("OrdenesServicio", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] OrdenServicioCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] OrdenServicioActualizarDTO obj)
        {
            var orden = await _application.ObtenerAsync(id);

            if (orden.Dato == null)
            {
                orden.Resultado = false;
                orden.Mensaje = NoEncontrada;
                return NotFound(orden);
            }

            var actualizar = await _application.ActualizarAsync(id, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        [HttpPost("{id:int}/Estado")]
        public async Task<ActionResult<Respuesta<bool>>> CambiarEstadoAsync([FromRoute] int id, [FromBody] OrdenServicioEstadoDTO obj)
        {
            var orden = await _application.ObtenerAsync(id);

            if (orden.Dato == null)
            {
                orden.Resultado = false;
                orden.Mensaje = NoEncontrada;
                return NotFound(orden);
            }

            var cambio = await _application.CambiarEstadoAsync(id, obj.Estado);

            if (!cambio.Resultado)
                return BadRequest(cambio);

            return Ok(cambio);
        }

        // Cancelar una orden equivale a anularla: exige Accion.Eliminar (convención: 'cancelar documento -> Eliminar').
        [RequierePermiso("OrdenesServicio", Accion.Eliminar)]
        [HttpPost("{id:int}/Cancelar")]
        public async Task<ActionResult<Respuesta<bool>>> CancelarAsync([FromRoute] int id)
        {
            var orden = await _application.ObtenerAsync(id);

            if (orden.Dato == null)
            {
                orden.Resultado = false;
                orden.Mensaje = NoEncontrada;
                return NotFound(orden);
            }

            var cancelar = await _application.CancelarAsync(id);

            if (!cancelar.Resultado)
                return BadRequest(cancelar);

            return Ok(cancelar);
        }
    }
}
