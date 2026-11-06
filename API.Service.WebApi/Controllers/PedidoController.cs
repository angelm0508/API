using API.Application.DTO;
using API.Application.DTO.pedido;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
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
        private readonly IAutorizacionCreditoContexto _autorizacionCredito;

        public PedidoController(IPedidoApplication pedidoApplication, IAutorizacionCreditoContexto autorizacionCredito)
        {
            _pedidoApplication = pedidoApplication;
            _autorizacionCredito = autorizacionCredito;
        }

        [RequierePermiso("Pedidos", Accion.Ver)]
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

        [RequierePermiso("Pedidos", Accion.Ver)]
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

        [RequierePermiso("Pedidos", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] PedidoCrearDTO obj)
        {
            var denegado = AutorizacionCreditoPeticion.Aplicar(User, obj.AutorizarExcesoCredito, _autorizacionCredito);
            if (denegado != null)
                return denegado;

            var insert = await _pedidoApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                if (insert is RespuestaCredito<int> { Credito: not null })
                    return Conflict(insert);
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [RequierePermiso("Pedidos", Accion.Actualizar)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] PedidoActualizarDTO obj)
        {
            var denegado = AutorizacionCreditoPeticion.Aplicar(User, obj.AutorizarExcesoCredito, _autorizacionCredito);
            if (denegado != null)
                return denegado;

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
                if (update is RespuestaCredito<bool> { Credito: not null })
                    return Conflict(update);
                return BadRequest(update);
            }

            return Ok(update);
        }

        [RequierePermiso("Pedidos", Accion.Eliminar)]
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
