using API.Application.DTO;
using API.Application.DTO.notaDebito;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/NotaDebito")]
    public class NotaDebitoController : ControllerBase
    {
        private readonly INotaDebitoApplication _application;

        public NotaDebitoController(INotaDebitoApplication application)
        {
            _application = application;
        }

        [RequierePermiso("NotasDebito", Accion.Ver)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<NotaDebitoDTO>>> Obtener([FromRoute] int id)
        {
            var nota = await _application.ObtenerAsync(id);

            if (!nota.Resultado)
            {
                return BadRequest(nota);
            }

            if (nota.Dato == null)
            {
                nota.Resultado = false;
                nota.Mensaje = "El código de la nota de débito no se encontró.";
                return NotFound(nota);
            }

            return Ok(nota);
        }

        [RequierePermiso("NotasDebito", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<NotaDebitoDTO>>>> ObtenerTodoAsync()
        {
            var notas = await _application.ObtenerTodoAsync();

            if (!notas.Resultado)
            {
                return BadRequest(notas);
            }

            return Ok(notas);
        }

        [RequierePermiso("NotasDebito", Accion.Ver)]
        [HttpGet("{id:int}/Lineas")]
        public async Task<ActionResult<Respuesta<IEnumerable<NotaDebitoDetalleDTO>>>> ObtenerLineas([FromRoute] int id)
        {
            var lineas = await _application.ObtenerLineasAsync(id);

            if (!lineas.Resultado)
            {
                return BadRequest(lineas);
            }

            return Ok(lineas);
        }

        [RequierePermiso("NotasDebito", Accion.Ver)]
        [HttpGet("FacturasBase/{codigoSn}")]
        public async Task<ActionResult<Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>>>> ObtenerFacturasBase([FromRoute] string codigoSn)
        {
            var facturas = await _application.ObtenerFacturasBaseAsync(codigoSn);

            if (!facturas.Resultado)
            {
                return BadRequest(facturas);
            }

            return Ok(facturas);
        }

        [RequierePermiso("NotasDebito", Accion.Ver)]
        [HttpGet("FacturaBase/{entry:int}")]
        public async Task<ActionResult<Respuesta<NotaDebitoFacturaBaseDTO>>> ObtenerFacturaBase([FromRoute] int entry)
        {
            var factura = await _application.ObtenerFacturaBaseAsync(entry);

            if (!factura.Resultado)
            {
                return BadRequest(factura);
            }

            return Ok(factura);
        }

        [RequierePermiso("NotasDebito", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] NotaDebitoCrearDTO obj)
        {
            var insert = await _application.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [RequierePermiso("NotasDebito", Accion.Actualizar)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] NotaDebitoActualizarDTO obj)
        {
            // Cancelar un documento (Cancelado = 'S') equivale a eliminarlo: exige Accion.Eliminar
            // ademas de Actualizar (spec de permisos: 'cancelar documento -> Eliminar').
            if (obj.Cancelado == "S" && !PermisoUsuario.Tiene(User, "NotasDebito", Accion.Eliminar))
                return PermisoUsuario.Denegado();

            var nota = await _application.ObtenerAsync(id);

            if (nota.Dato == null)
            {
                nota.Resultado = false;
                nota.Mensaje = "El código de la nota de débito no se encontró.";
                return NotFound(nota);
            }

            var update = await _application.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [RequierePermiso("NotasDebito", Accion.Eliminar)]
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var nota = await _application.ObtenerAsync(id);

            if (nota.Dato == null)
            {
                nota.Resultado = false;
                nota.Mensaje = "El código de la nota de débito no se encontró.";
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
