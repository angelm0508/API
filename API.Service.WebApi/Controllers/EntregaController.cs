using API.Application.DTO;
using API.Application.DTO.entrega;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Entrega")]
    public class EntregaController : ControllerBase
    {
        private readonly IEntregaApplication _entregaApplication;

        public EntregaController(IEntregaApplication entregaApplication)
        {
            _entregaApplication = entregaApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<EntregaDTO>>> Obtener([FromRoute] int id)
        {
            var entrega = await _entregaApplication.ObtenerAsync(id);

            if (!entrega.Resultado)
            {
                return BadRequest(entrega);
            }

            if (entrega.Dato == null)
            {
                entrega.Resultado = false;
                entrega.Mensaje = "El código de la entrega no se encontró.";
                return NotFound(entrega);
            }

            return Ok(entrega);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<EntregaDTO>>>> ObtenerTodoAsync()
        {
            var entregas = await _entregaApplication.ObtenerTodoAsync();

            if (!entregas.Resultado)
            {
                return BadRequest(entregas);
            }

            return Ok(entregas);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] EntregaCrearDTO obj)
        {
            var insert = await _entregaApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] EntregaActualizarDTO obj)
        {
            var entrega = await _entregaApplication.ObtenerAsync(id);

            if (entrega.Dato == null)
            {
                entrega.Resultado = false;
                entrega.Mensaje = "El código de la entrega no se encontró.";
                return NotFound(entrega);
            }

            var update = await _entregaApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var entrega = await _entregaApplication.ObtenerAsync(id);

            if (entrega.Dato == null)
            {
                entrega.Resultado = false;
                entrega.Mensaje = "El código de la entrega no se encontró.";
                return NotFound(entrega);
            }

            var delete = await _entregaApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
