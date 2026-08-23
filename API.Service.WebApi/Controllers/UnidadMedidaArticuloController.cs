using API.Application.DTO;
using API.Application.DTO.articulo.unidad_medida_articulo;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/UnidadMedidaArticulo")]
    public class UnidadMedidaArticuloController : ControllerBase
    {
        private readonly IUnidadMedidaArticuloApplication _unidadMedidaArticuloApplication;

        public UnidadMedidaArticuloController(IUnidadMedidaArticuloApplication unidadMedidaArticuloApplication)
        {
            _unidadMedidaArticuloApplication = unidadMedidaArticuloApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<UnidadMedidaArticuloDTO>>> Obtener([FromRoute] int id)
        {
            var medida = await _unidadMedidaArticuloApplication.ObtenerAsync(id);

            if (!medida.Resultado)
            {
                return BadRequest(medida);
            }

            if (medida.Dato == null)
            {
                medida.Resultado = false;
                medida.Mensaje = "El código de la medida no se encontró.";
                return NotFound(medida);
            }

            return Ok(medida);
        }

        [HttpGet("PorCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<UnidadMedidaArticuloDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var medida = await _unidadMedidaArticuloApplication.ObtenerAsync(codigo);

            if (!medida.Resultado)
            {
                return BadRequest(medida);
            }

            if (medida.Dato == null)
            {
                medida.Resultado = false;
                medida.Mensaje = "El código de la medida no se encontró.";
                return NotFound(medida);
            }

            return Ok(medida);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<Respuesta<IEnumerable<UnidadMedidaArticuloDTO>>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var medidas = await _unidadMedidaArticuloApplication.ObtenerContengaNombreAsync(name);

            if (!medidas.Resultado)
            {
                return BadRequest(medidas);
            }

            return Ok(medidas);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<UnidadMedidaArticuloDTO>>>> ObtenerTodoAsync()
        {
            var medidas = await _unidadMedidaArticuloApplication.ObtenerTodoAsync();

            if (!medidas.Resultado)
            {
                return BadRequest(medidas);
            }

            return Ok(medidas);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] UnidadMedidaArticuloCrearDTO obj)
        {
            var insert = await _unidadMedidaArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] UnidadMedidaArticuloActualizarDTO obj)
        {
            var medida = await _unidadMedidaArticuloApplication.ObtenerAsync(id);

            if (medida.Dato == null)
            {
                medida.Resultado = false;
                medida.Mensaje = "El código de la medida no se encontró.";
                return NotFound(medida);
            }

            var update = await _unidadMedidaArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var medida = await _unidadMedidaArticuloApplication.ObtenerAsync(id);

            if (medida.Dato == null)
            {
                medida.Resultado = false;
                medida.Mensaje = "El código de la medida no se encontró.";
                return NotFound(medida);
            }

            var delete = await _unidadMedidaArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
