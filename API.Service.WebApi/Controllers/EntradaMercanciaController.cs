using API.Application.DTO;
using API.Application.DTO.entradaMercancia;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
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

        [RequierePermiso("EntradasMercancia", Accion.Ver)]
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

        [RequierePermiso("EntradasMercancia", Accion.Ver)]
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

        [RequierePermiso("EntradasMercancia", Accion.Agregar)]
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

        [RequierePermiso("EntradasMercancia", Accion.Actualizar)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] EntradaMercanciaActualizarDTO obj)
        {
            // Cancelar un documento (Cancelado = 'S') equivale a eliminarlo: exige Accion.Eliminar
            // ademas de Actualizar (spec: 'cancelar documento -> Eliminar').
            if (obj.Cancelado == "S" && !PermisoUsuario.Tiene(User, "EntradasMercancia", Accion.Eliminar))
                return PermisoUsuario.Denegado();

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

        [RequierePermiso("EntradasMercancia", Accion.Eliminar)]
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
