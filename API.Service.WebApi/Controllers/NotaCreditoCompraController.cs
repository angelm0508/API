using API.Application.DTO;
using API.Application.DTO.notaCreditoCompra;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/NotaCreditoCompra")]
    public class NotaCreditoCompraController : ControllerBase
    {
        private readonly INotaCreditoCompraApplication _application;

        public NotaCreditoCompraController(INotaCreditoCompraApplication application)
        {
            _application = application;
        }

        [RequierePermiso("NotasCreditoCompra", Accion.Ver)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<NotaCreditoCompraDTO>>> Obtener([FromRoute] int id)
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

        [RequierePermiso("NotasCreditoCompra", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<NotaCreditoCompraDTO>>>> ObtenerTodoAsync()
        {
            var notas = await _application.ObtenerTodoAsync();

            if (!notas.Resultado)
            {
                return BadRequest(notas);
            }

            return Ok(notas);
        }

        [RequierePermiso("NotasCreditoCompra", Accion.Ver)]
        [HttpGet("{id:int}/Lineas")]
        public async Task<ActionResult<Respuesta<IEnumerable<NotaCreditoCompraDetalleDTO>>>> ObtenerLineas([FromRoute] int id)
        {
            var lineas = await _application.ObtenerLineasAsync(id);

            if (!lineas.Resultado)
            {
                return BadRequest(lineas);
            }

            return Ok(lineas);
        }

        [RequierePermiso("NotasCreditoCompra", Accion.Agregar)]
        [HttpGet("FacturasCompraBase/{codigoSn}")]
        public async Task<ActionResult<Respuesta<IEnumerable<NotaCreditoCompraFacturaBaseResumenDTO>>>> ObtenerFacturasCompraBase([FromRoute] string codigoSn)
        {
            var facturas = await _application.ObtenerFacturasCompraBaseAsync(codigoSn);

            if (!facturas.Resultado)
            {
                return BadRequest(facturas);
            }

            return Ok(facturas);
        }

        [RequierePermiso("NotasCreditoCompra", Accion.Agregar)]
        [HttpGet("FacturaCompraBase/{entry:int}")]
        public async Task<ActionResult<Respuesta<NotaCreditoCompraFacturaBaseDTO>>> ObtenerFacturaCompraBase([FromRoute] int entry)
        {
            var factura = await _application.ObtenerFacturaCompraBaseAsync(entry);

            if (!factura.Resultado)
            {
                return BadRequest(factura);
            }

            return Ok(factura);
        }

        [RequierePermiso("NotasCreditoCompra", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] NotaCreditoCompraCrearDTO obj)
        {
            var insert = await _application.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [RequierePermiso("NotasCreditoCompra", Accion.Actualizar)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] NotaCreditoCompraActualizarDTO obj)
        {
            // Cancelar un documento (Cancelado = 'S') equivale a eliminarlo: exige Accion.Eliminar
            // ademas de Actualizar (spec de permisos: 'cancelar documento -> Eliminar').
            if (obj.Cancelado == "S" && !PermisoUsuario.Tiene(User, "NotasCreditoCompra", Accion.Eliminar))
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

        [RequierePermiso("NotasCreditoCompra", Accion.Eliminar)]
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
