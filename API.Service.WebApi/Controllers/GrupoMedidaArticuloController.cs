using API.Application.DTO;
using API.Application.DTO.articulo.grupo_medida_articulo;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/GrupoMedidaArticulo")]
    public class GrupoMedidaArticuloController : ControllerBase
    {
        private readonly IGrupoMedidaArticuloApplication _grupoMedidaArticuloApplication;

        public GrupoMedidaArticuloController(IGrupoMedidaArticuloApplication grupoMedidaArticuloApplication)
        {
            _grupoMedidaArticuloApplication = grupoMedidaArticuloApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<GrupoMedidaArticuloDTO>>> Obtener([FromRoute] int id)
        {
            var grupoMedida = await _grupoMedidaArticuloApplication.ObtenerAsync(id);

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
        public async Task<ActionResult<Respuesta<GrupoMedidaArticuloDTO>>> ObtenerPorNombre([FromRoute] string name)
        {
            var grupoMedida = await _grupoMedidaArticuloApplication.ObtenerAsync(name);

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
        public async Task<ActionResult<Respuesta<IEnumerable<GrupoMedidaArticuloDTO>>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var gruposMedida = await _grupoMedidaArticuloApplication.ObtenerContengaNombreAsync(name);

            if (!gruposMedida.Resultado)
            {
                return BadRequest(gruposMedida);
            }

            return Ok(gruposMedida);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<GrupoMedidaArticuloDTO>>>> ObtenerTodoAsync()
        {
            var gruposMedida = await _grupoMedidaArticuloApplication.ObtenerTodoAsync();

            if (!gruposMedida.Resultado)
            {
                return BadRequest(gruposMedida);
            }

            return Ok(gruposMedida);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] GrupoMedidaArticuloCrearDTO obj)
        {
            var insert = await _grupoMedidaArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] GrupoMedidaArticuloActualizarDTO obj)
        {
            var grupoMedida = await _grupoMedidaArticuloApplication.ObtenerAsync(id);

            if (grupoMedida.Dato == null)
            {
                grupoMedida.Resultado = false;
                grupoMedida.Mensaje = "El código del grupo de medida no se encontró.";
                return NotFound(grupoMedida);
            }

            var update = await _grupoMedidaArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var grupoMedida = await _grupoMedidaArticuloApplication.ObtenerAsync(id);

            if (grupoMedida.Dato == null)
            {
                grupoMedida.Resultado = false;
                grupoMedida.Mensaje = "El código del grupo de medida no se encontró.";
                return NotFound(grupoMedida);
            }

            var delete = await _grupoMedidaArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
