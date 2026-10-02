using API.Application.DTO;
using API.Application.DTO.entrega;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
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

        [RequierePermiso("Entregas", Accion.Ver)]
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

        [RequierePermiso("Entregas", Accion.Ver)]
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

        [RequierePermiso("Entregas", Accion.Agregar)]
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

        [RequierePermiso("Entregas", Accion.Actualizar)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] EntregaActualizarDTO obj)
        {
            // Cancelar un documento (Cancelado = 'S') equivale a eliminarlo: exige Accion.Eliminar
            // ademas de Actualizar (spec: 'cancelar documento -> Eliminar').
            if (obj.Cancelado == "S" && !PermisoUsuario.Tiene(User, "Entregas", Accion.Eliminar))
                return PermisoUsuario.Denegado();

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

        [RequierePermiso("Entregas", Accion.Eliminar)]
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
