using API.Application.DTO;
using API.Application.DTO.entregaCompra;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/EntregaCompra")]
    public class EntregaCompraController : ControllerBase
    {
        private readonly IEntregaCompraApplication _entregaCompraApplication;

        public EntregaCompraController(IEntregaCompraApplication entregaCompraApplication)
        {
            _entregaCompraApplication = entregaCompraApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<EntregaCompraDTO>>> Obtener([FromRoute] int id)
        {
            var entregaCompra = await _entregaCompraApplication.ObtenerAsync(id);

            if (!entregaCompra.Resultado)
            {
                return BadRequest(entregaCompra);
            }

            if (entregaCompra.Dato == null)
            {
                entregaCompra.Resultado = false;
                entregaCompra.Mensaje = "El código de la entrega de compra no se encontró.";
                return NotFound(entregaCompra);
            }

            return Ok(entregaCompra);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<EntregaCompraDTO>>>> ObtenerTodoAsync()
        {
            var entregaCompras = await _entregaCompraApplication.ObtenerTodoAsync();

            if (!entregaCompras.Resultado)
            {
                return BadRequest(entregaCompras);
            }

            return Ok(entregaCompras);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] EntregaCompraCrearDTO obj)
        {
            var insert = await _entregaCompraApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] EntregaCompraActualizarDTO obj)
        {
            var entregaCompra = await _entregaCompraApplication.ObtenerAsync(id);

            if (entregaCompra.Dato == null)
            {
                entregaCompra.Resultado = false;
                entregaCompra.Mensaje = "El código de la entrega de compra no se encontró.";
                return NotFound(entregaCompra);
            }

            var update = await _entregaCompraApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var entregaCompra = await _entregaCompraApplication.ObtenerAsync(id);

            if (entregaCompra.Dato == null)
            {
                entregaCompra.Resultado = false;
                entregaCompra.Mensaje = "El código de la entrega de compra no se encontró.";
                return NotFound(entregaCompra);
            }

            var delete = await _entregaCompraApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
