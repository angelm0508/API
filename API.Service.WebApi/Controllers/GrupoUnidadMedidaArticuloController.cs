using API.Application.DTO;
using API.Application.DTO.articulo.grupo_unidad_medida_articulo;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/GrupoUnidadMedidaArticulo")]
    public class GrupoUnidadMedidaArticuloController : ControllerBase
    {
        private readonly IGrupoUnidadMedidaArticuloApplication _grupoUnidadMedidaArticuloApplication;

        public GrupoUnidadMedidaArticuloController(IGrupoUnidadMedidaArticuloApplication grupoUnidadMedidaArticuloApplication)
        {
            _grupoUnidadMedidaArticuloApplication = grupoUnidadMedidaArticuloApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<GrupoUnidadMedidaArticuloDTO>>> Obtener([FromRoute] int id)
        {
            var grupoMedida = await _grupoUnidadMedidaArticuloApplication.ObtenerAsync(id);

            if (!grupoMedida.Resultado)
            {
                return BadRequest(grupoMedida);
            }

            if (grupoMedida.Dato == null)
            {
                grupoMedida.Resultado = false;
                grupoMedida.Mensaje = "El código del grupo de medida no se encontró.";
                return NotFound(grupoMedida);
            }

            return Ok(grupoMedida);
        }

        [HttpGet("PorNombre/{name}")]
        public async Task<ActionResult<Respuesta<GrupoUnidadMedidaArticuloDTO>>> ObtenerPorNombre([FromRoute] string name)
        {
            var grupoMedida = await _grupoUnidadMedidaArticuloApplication.ObtenerAsync(name);

            if (!grupoMedida.Resultado)
            {
                return BadRequest(grupoMedida);
            }

            if (grupoMedida.Dato == null)
            {
                grupoMedida.Resultado = false;
                grupoMedida.Mensaje = "El nombre del grupo de medida no se encontró.";
                return NotFound(grupoMedida);
            }

            return Ok(grupoMedida);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<Respuesta<IEnumerable<GrupoUnidadMedidaArticuloDTO>>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var gruposMedida = await _grupoUnidadMedidaArticuloApplication.ObtenerContengaNombreAsync(name);

            if (!gruposMedida.Resultado)
            {
                return BadRequest(gruposMedida);
            }

            return Ok(gruposMedida);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<GrupoUnidadMedidaArticuloDTO>>>> ObtenerTodoAsync()
        {
            var gruposMedida = await _grupoUnidadMedidaArticuloApplication.ObtenerTodoAsync();

            if (!gruposMedida.Resultado)
            {
                return BadRequest(gruposMedida);
            }

            return Ok(gruposMedida);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] GrupoUnidadMedidaArticuloCrearDTO obj)
        {
            var insert = await _grupoUnidadMedidaArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] GrupoUnidadMedidaArticuloActualizarDTO obj)
        {
            var grupoMedida = await _grupoUnidadMedidaArticuloApplication.ObtenerAsync(id);

            if (grupoMedida.Dato == null)
            {
                grupoMedida.Resultado = false;
                grupoMedida.Mensaje = "El código del grupo de medida no se encontró.";
                return NotFound(grupoMedida);
            }

            var update = await _grupoUnidadMedidaArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var grupoMedida = await _grupoUnidadMedidaArticuloApplication.ObtenerAsync(id);

            if (grupoMedida.Dato == null)
            {
                grupoMedida.Resultado = false;
                grupoMedida.Mensaje = "El código del grupo de medida no se encontró.";
                return NotFound(grupoMedida);
            }

            var delete = await _grupoUnidadMedidaArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
