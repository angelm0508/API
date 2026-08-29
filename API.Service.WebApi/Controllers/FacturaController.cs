using API.Application.DTO;
using API.Application.DTO.factura;
using API.Application.Interface;
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
                factura.Mensaje = "El código del factura no se encontró.";
                return NotFound(factura);
            }

            return Ok(factura);
        }

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

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] FacturaActualizarDTO obj)
        {
            var factura = await _facturaApplication.ObtenerAsync(id);

            if (factura.Dato == null)
            {
                factura.Resultado = false;
                factura.Mensaje = "El código del factura no se encontró.";
                return NotFound(factura);
            }

            var update = await _facturaApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var factura = await _facturaApplication.ObtenerAsync(id);

            if (factura.Dato == null)
            {
                factura.Resultado = false;
                factura.Mensaje = "El código del factura no se encontró.";
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
