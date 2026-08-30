using API.Application.DTO;
using API.Application.DTO.facturaCompra;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/FacturaCompra")]
    public class FacturaCompraController : ControllerBase
    {
        private readonly IFacturaCompraApplication _facturaCompraApplication;

        public FacturaCompraController(IFacturaCompraApplication facturaCompraApplication)
        {
            _facturaCompraApplication = facturaCompraApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<FacturaCompraDTO>>> Obtener([FromRoute] int id)
        {
            var facturaCompra = await _facturaCompraApplication.ObtenerAsync(id);

            if (!facturaCompra.Resultado)
            {
                return BadRequest(facturaCompra);
            }

            if (facturaCompra.Dato == null)
            {
                facturaCompra.Resultado = false;
                facturaCompra.Mensaje = "El código de la factura de compra no se encontró.";
                return NotFound(facturaCompra);
            }

            return Ok(facturaCompra);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<FacturaCompraDTO>>>> ObtenerTodoAsync()
        {
            var facturasCompra = await _facturaCompraApplication.ObtenerTodoAsync();

            if (!facturasCompra.Resultado)
            {
                return BadRequest(facturasCompra);
            }

            return Ok(facturasCompra);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] FacturaCompraCrearDTO obj)
        {
            var insert = await _facturaCompraApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] FacturaCompraActualizarDTO obj)
        {
            var facturaCompra = await _facturaCompraApplication.ObtenerAsync(id);

            if (facturaCompra.Dato == null)
            {
                facturaCompra.Resultado = false;
                facturaCompra.Mensaje = "El código de la factura de compra no se encontró.";
                return NotFound(facturaCompra);
            }

            var update = await _facturaCompraApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var facturaCompra = await _facturaCompraApplication.ObtenerAsync(id);

            if (facturaCompra.Dato == null)
            {
                facturaCompra.Resultado = false;
                facturaCompra.Mensaje = "El código de la factura de compra no se encontró.";
                return NotFound(facturaCompra);
            }

            var delete = await _facturaCompraApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
