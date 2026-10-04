using API.Application.DTO;
using API.Application.DTO.notaCredito;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/NotaCredito")]
    public class NotaCreditoController : ControllerBase
    {
        private readonly INotaCreditoApplication _application;

        public NotaCreditoController(INotaCreditoApplication application)
        {
            _application = application;
        }

        [RequierePermiso("NotasCredito", Accion.Ver)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<NotaCreditoDTO>>> Obtener([FromRoute] int id)
        {
            var nota = await _application.ObtenerAsync(id);

            if (!nota.Resultado)
            {
                return BadRequest(nota);
            }

            if (nota.Dato == null)
            {
                nota.Resultado = false;
                nota.Mensaje = "El código de la nota de crédito no se encontró.";
                return NotFound(nota);
            }

            return Ok(nota);
        }

        [RequierePermiso("NotasCredito", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<NotaCreditoDTO>>>> ObtenerTodoAsync()
        {
            var notas = await _application.ObtenerTodoAsync();

            if (!notas.Resultado)
            {
                return BadRequest(notas);
            }

            return Ok(notas);
        }

        [RequierePermiso("NotasCredito", Accion.Ver)]
        [HttpGet("{id:int}/Lineas")]
        public async Task<ActionResult<Respuesta<IEnumerable<NotaCreditoDetalleDTO>>>> ObtenerLineas([FromRoute] int id)
        {
            var lineas = await _application.ObtenerLineasAsync(id);

            if (!lineas.Resultado)
            {
                return BadRequest(lineas);
            }

            return Ok(lineas);
        }

        [RequierePermiso("NotasCredito", Accion.Agregar)]
        [HttpGet("FacturasBase/{codigoSn}")]
        public async Task<ActionResult<Respuesta<IEnumerable<NotaCreditoFacturaBaseResumenDTO>>>> ObtenerFacturasBase([FromRoute] string codigoSn)
        {
            var facturas = await _application.ObtenerFacturasBaseAsync(codigoSn);

            if (!facturas.Resultado)
            {
                return BadRequest(facturas);
            }

            return Ok(facturas);
        }

        [RequierePermiso("NotasCredito", Accion.Agregar)]
        [HttpGet("FacturaBase/{entry:int}")]
        public async Task<ActionResult<Respuesta<NotaCreditoFacturaBaseDTO>>> ObtenerFacturaBase([FromRoute] int entry)
        {
            var factura = await _application.ObtenerFacturaBaseAsync(entry);

            if (!factura.Resultado)
            {
                return BadRequest(factura);
            }

            return Ok(factura);
        }

        [RequierePermiso("NotasCredito", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] NotaCreditoCrearDTO obj)
        {
            var insert = await _application.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [RequierePermiso("NotasCredito", Accion.Actualizar)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] NotaCreditoActualizarDTO obj)
        {
            // Cancelar un documento (Cancelado = 'S') equivale a eliminarlo: exige Accion.Eliminar
            // ademas de Actualizar (spec de permisos: 'cancelar documento -> Eliminar').
            if (obj.Cancelado == "S" && !PermisoUsuario.Tiene(User, "NotasCredito", Accion.Eliminar))
                return PermisoUsuario.Denegado();

            var nota = await _application.ObtenerAsync(id);

            if (nota.Dato == null)
            {
                nota.Resultado = false;
                nota.Mensaje = "El código de la nota de crédito no se encontró.";
                return NotFound(nota);
            }

            var update = await _application.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [RequierePermiso("NotasCredito", Accion.Eliminar)]
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var nota = await _application.ObtenerAsync(id);

            if (nota.Dato == null)
            {
                nota.Resultado = false;
                nota.Mensaje = "El código de la nota de crédito no se encontró.";
                return NotFound(nota);
            }

            var delete = await _application.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
