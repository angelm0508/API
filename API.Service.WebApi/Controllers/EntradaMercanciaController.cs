using API.Application.DTO;
using API.Application.DTO.entradaMercancia;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/EntradaMercancia")]
    public class EntradaMercanciaController : ControllerBase
    {
        private readonly IEntradaMercanciaApplication _entradaMercanciaApplication;

        public EntradaMercanciaController(IEntradaMercanciaApplication entradaMercanciaApplication)
        {
            _entradaMercanciaApplication = entradaMercanciaApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<EntradaMercanciaDTO>>> Obtener([FromRoute] int id)
        {
            var entradaMercancia = await _entradaMercanciaApplication.ObtenerAsync(id);

            if (!entradaMercancia.Resultado)
            {
                return BadRequest(entradaMercancia);
            }

            if (entradaMercancia.Dato == null)
            {
                entradaMercancia.Resultado = false;
                entradaMercancia.Mensaje = "El código de la entrada de mercancía no se encontró.";
                return NotFound(entradaMercancia);
            }

            return Ok(entradaMercancia);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<EntradaMercanciaDTO>>>> ObtenerTodoAsync()
        {
            var entradaMercancias = await _entradaMercanciaApplication.ObtenerTodoAsync();

            if (!entradaMercancias.Resultado)
            {
                return BadRequest(entradaMercancias);
            }

            return Ok(entradaMercancias);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] EntradaMercanciaCrearDTO obj)
        {
            var insert = await _entradaMercanciaApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] EntradaMercanciaActualizarDTO obj)
        {
            var entradaMercancia = await _entradaMercanciaApplication.ObtenerAsync(id);

            if (entradaMercancia.Dato == null)
            {
                entradaMercancia.Resultado = false;
                entradaMercancia.Mensaje = "El código de la entrada de mercancía no se encontró.";
                return NotFound(entradaMercancia);
            }

            var update = await _entradaMercanciaApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var entradaMercancia = await _entradaMercanciaApplication.ObtenerAsync(id);

            if (entradaMercancia.Dato == null)
            {
                entradaMercancia.Resultado = false;
                entradaMercancia.Mensaje = "El código de la entrada de mercancía no se encontró.";
                return NotFound(entradaMercancia);
            }

            var delete = await _entradaMercanciaApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
