using API.Application.DTO;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    // Agregar, cambiar o quitar una línea es editar la orden: los tres endpoints exigen Actualizar sobre OrdenesServicio.
    [Authorize]
    [ApiController]
    [Route("api/OrdenServicioDetalle")]
    public class OrdenServicioDetalleController : ControllerBase
    {
        private readonly IOrdenServicioDetalleApplication _application;

        public OrdenServicioDetalleController(IOrdenServicioDetalleApplication application)
        {
            _application = application;
        }

        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] OrdenServicioDetalleCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        [HttpPut("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int entry, [FromRoute] int noLinea, [FromBody] OrdenServicioDetalleActualizarDTO obj)
        {
            var actualizar = await _application.ActualizarAsync(entry, noLinea, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        [HttpDelete("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var eliminar = await _application.EliminarAsync(entry, noLinea);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
