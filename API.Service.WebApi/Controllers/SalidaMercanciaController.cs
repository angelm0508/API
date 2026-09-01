using API.Application.DTO;
using API.Application.DTO.salidaMercancia;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/SalidaMercancia")]
    public class SalidaMercanciaController : ControllerBase
    {
        private readonly ISalidaMercanciaApplication _salidaMercanciaApplication;

        public SalidaMercanciaController(ISalidaMercanciaApplication salidaMercanciaApplication)
        {
            _salidaMercanciaApplication = salidaMercanciaApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<SalidaMercanciaDTO>>> Obtener([FromRoute] int id)
        {
            var salidaMercancia = await _salidaMercanciaApplication.ObtenerAsync(id);

            if (!salidaMercancia.Resultado)
            {
                return BadRequest(salidaMercancia);
            }

            if (salidaMercancia.Dato == null)
            {
                salidaMercancia.Resultado = false;
                salidaMercancia.Mensaje = "El código de la salida de mercancía no se encontró.";
                return NotFound(salidaMercancia);
            }

            return Ok(salidaMercancia);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<SalidaMercanciaDTO>>>> ObtenerTodoAsync()
        {
            var salidaMercancias = await _salidaMercanciaApplication.ObtenerTodoAsync();

            if (!salidaMercancias.Resultado)
            {
                return BadRequest(salidaMercancias);
            }

            return Ok(salidaMercancias);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] SalidaMercanciaCrearDTO obj)
        {
            var insert = await _salidaMercanciaApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] SalidaMercanciaActualizarDTO obj)
        {
            var salidaMercancia = await _salidaMercanciaApplication.ObtenerAsync(id);

            if (salidaMercancia.Dato == null)
            {
                salidaMercancia.Resultado = false;
                salidaMercancia.Mensaje = "El código de la salida de mercancía no se encontró.";
                return NotFound(salidaMercancia);
            }

            var update = await _salidaMercanciaApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var salidaMercancia = await _salidaMercanciaApplication.ObtenerAsync(id);

            if (salidaMercancia.Dato == null)
            {
                salidaMercancia.Resultado = false;
                salidaMercancia.Mensaje = "El código de la salida de mercancía no se encontró.";
                return NotFound(salidaMercancia);
            }

            var delete = await _salidaMercanciaApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
