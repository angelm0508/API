using API.Application.DTO;
using API.Application.DTO.pedido;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Pedido")]
    public class PedidoController : ControllerBase
    {
        private readonly IPedidoApplication _pedidoApplication;

        public PedidoController(IPedidoApplication pedidoApplication)
        {
            _pedidoApplication = pedidoApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<PedidoDTO>>> Obtener([FromRoute] int id)
        {
            var pedido = await _pedidoApplication.ObtenerAsync(id);

            if (!pedido.Resultado)
            {
                return BadRequest(pedido);
            }

            if (pedido.Dato == null)
            {
                pedido.Resultado = false;
                pedido.Mensaje = "El código del pedido no se encontró.";
                return NotFound(pedido);
            }

            return Ok(pedido);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<PedidoDTO>>>> ObtenerTodoAsync()
        {
            var pedidos = await _pedidoApplication.ObtenerTodoAsync();

            if (!pedidos.Resultado)
            {
                return BadRequest(pedidos);
            }

            return Ok(pedidos);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] PedidoCrearDTO obj)
        {
            var insert = await _pedidoApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] PedidoActualizarDTO obj)
        {
            var pedido = await _pedidoApplication.ObtenerAsync(id);

            if (pedido.Dato == null)
            {
                pedido.Resultado = false;
                pedido.Mensaje = "El código del pedido no se encontró.";
                return NotFound(pedido);
            }

            var update = await _pedidoApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var pedido = await _pedidoApplication.ObtenerAsync(id);

            if (pedido.Dato == null)
            {
                pedido.Resultado = false;
                pedido.Mensaje = "El código del pedido no se encontró.";
                return NotFound(pedido);
            }

            var delete = await _pedidoApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
