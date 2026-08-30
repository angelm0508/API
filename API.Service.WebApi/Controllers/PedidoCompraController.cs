using API.Application.DTO;
using API.Application.DTO.pedidoCompra;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/PedidoCompra")]
    public class PedidoCompraController : ControllerBase
    {
        private readonly IPedidoCompraApplication _pedidoCompraApplication;

        public PedidoCompraController(IPedidoCompraApplication pedidoCompraApplication)
        {
            _pedidoCompraApplication = pedidoCompraApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<PedidoCompraDTO>>> Obtener([FromRoute] int id)
        {
            var pedidoCompra = await _pedidoCompraApplication.ObtenerAsync(id);

            if (!pedidoCompra.Resultado)
            {
                return BadRequest(pedidoCompra);
            }

            if (pedidoCompra.Dato == null)
            {
                pedidoCompra.Resultado = false;
                pedidoCompra.Mensaje = "El código del pedido de compra no se encontró.";
                return NotFound(pedidoCompra);
            }

            return Ok(pedidoCompra);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<PedidoCompraDTO>>>> ObtenerTodoAsync()
        {
            var pedidoCompras = await _pedidoCompraApplication.ObtenerTodoAsync();

            if (!pedidoCompras.Resultado)
            {
                return BadRequest(pedidoCompras);
            }

            return Ok(pedidoCompras);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] PedidoCompraCrearDTO obj)
        {
            var insert = await _pedidoCompraApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] PedidoCompraActualizarDTO obj)
        {
            var pedidoCompra = await _pedidoCompraApplication.ObtenerAsync(id);

            if (pedidoCompra.Dato == null)
            {
                pedidoCompra.Resultado = false;
                pedidoCompra.Mensaje = "El código del pedido de compra no se encontró.";
                return NotFound(pedidoCompra);
            }

            var update = await _pedidoCompraApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var pedidoCompra = await _pedidoCompraApplication.ObtenerAsync(id);

            if (pedidoCompra.Dato == null)
            {
                pedidoCompra.Resultado = false;
                pedidoCompra.Mensaje = "El código del pedido de compra no se encontró.";
                return NotFound(pedidoCompra);
            }

            var delete = await _pedidoCompraApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
