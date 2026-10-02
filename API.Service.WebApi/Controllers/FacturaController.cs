using API.Application.DTO;
using API.Application.DTO.factura;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Factura")]
    public class FacturaController : ControllerBase
    {
        private readonly IFacturaApplication _facturaApplication;

        public FacturaController(IFacturaApplication facturaApplication)
        {
            _facturaApplication = facturaApplication;
        }

        [RequierePermiso("Facturas", Accion.Ver)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<FacturaDTO>>> Obtener([FromRoute] int id)
        {
            var factura = await _facturaApplication.ObtenerAsync(id);

            if (!factura.Resultado)
            {
                return BadRequest(factura);
            }

            if (factura.Dato == null)
            {
                factura.Resultado = false;
                factura.Mensaje = "El código de la factura no se encontró.";
                return NotFound(factura);
            }

            return Ok(factura);
        }

        [RequierePermiso("Facturas", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<FacturaDTO>>>> ObtenerTodoAsync()
        {
            var facturas = await _facturaApplication.ObtenerTodoAsync();

            if (!facturas.Resultado)
            {
                return BadRequest(facturas);
            }

            return Ok(facturas);
        }

        [RequierePermiso("Facturas", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] FacturaCrearDTO obj)
        {
            var insert = await _facturaApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [RequierePermiso("Facturas", Accion.Actualizar)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] FacturaActualizarDTO obj)
        {
            // Cancelar un documento (Cancelado = 'S') equivale a eliminarlo: exige Accion.Eliminar
            // ademas de Actualizar (spec: 'cancelar documento -> Eliminar').
            if (obj.Cancelado == "S" && !PermisoUsuario.Tiene(User, "Facturas", Accion.Eliminar))
                return PermisoUsuario.Denegado();

            var factura = await _facturaApplication.ObtenerAsync(id);

            if (factura.Dato == null)
            {
                factura.Resultado = false;
                factura.Mensaje = "El código de la factura no se encontró.";
                return NotFound(factura);
            }

            var update = await _facturaApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [RequierePermiso("Facturas", Accion.Eliminar)]
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var factura = await _facturaApplication.ObtenerAsync(id);

            if (factura.Dato == null)
            {
                factura.Resultado = false;
                factura.Mensaje = "El código de la factura no se encontró.";
                return NotFound(factura);
            }

            var delete = await _facturaApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
