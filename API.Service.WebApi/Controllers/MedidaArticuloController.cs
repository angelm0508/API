using API.Application.DTO;
using API.Application.DTO.articulo.medida_articulo;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/MedidaArticulo")]
    public class MedidaArticuloController : ControllerBase
    {
        private readonly IMedidaArticuloApplication _medidaArticuloApplication;

        public MedidaArticuloController(IMedidaArticuloApplication medidaArticuloApplication)
        {
            _medidaArticuloApplication = medidaArticuloApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<MedidaArticuloDTO>>> Obtener([FromRoute] int id)
        {
            var medida = await _medidaArticuloApplication.ObtenerAsync(id);

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
        public async Task<ActionResult<Respuesta<MedidaArticuloDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var medida = await _medidaArticuloApplication.ObtenerAsync(codigo);

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
        public async Task<ActionResult<Respuesta<IEnumerable<MedidaArticuloDTO>>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var medidas = await _medidaArticuloApplication.ObtenerContengaNombreAsync(name);

            if (!medidas.Resultado)
            {
                return BadRequest(medidas);
            }

            return Ok(medidas);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<MedidaArticuloDTO>>>> ObtenerTodoAsync()
        {
            var medidas = await _medidaArticuloApplication.ObtenerTodoAsync();

            if (!medidas.Resultado)
            {
                return BadRequest(medidas);
            }

            return Ok(medidas);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] MedidaArticuloCrearDTO obj)
        {
            var insert = await _medidaArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] MedidaArticuloActualizarDTO obj)
        {
            var medida = await _medidaArticuloApplication.ObtenerAsync(id);

            if (medida.Dato == null)
            {
                medida.Resultado = false;
                medida.Mensaje = "El código de la medida no se encontró.";
                return NotFound(medida);
            }

            var update = await _medidaArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var medida = await _medidaArticuloApplication.ObtenerAsync(id);

            if (medida.Dato == null)
            {
                medida.Resultado = false;
                medida.Mensaje = "El código de la medida no se encontró.";
                return NotFound(medida);
            }

            var delete = await _medidaArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
