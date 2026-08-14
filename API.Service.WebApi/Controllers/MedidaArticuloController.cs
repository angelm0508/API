using API.Application.DTO.articulo.medida_articulo;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
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
        public async Task<ActionResult<MedidaArticuloDTO>> Obtener([FromRoute] int id)
        {
            var medida = await _medidaArticuloApplication.ObtenerAsync(id);

            if (!medida.Resultado)
            {
                return BadRequest(new RespuestaError($"{medida.Mensaje}"));
            }

            if (medida.Dato == null)
            {
                return NotFound(new RespuestaError("El código de la medida no se encontró."));
            }

            return Ok(medida.Dato);
        }

        [HttpGet("PorCodigo/{codigo}")]
        public async Task<ActionResult<MedidaArticuloDTO>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var medida = await _medidaArticuloApplication.ObtenerAsync(codigo);

            if (!medida.Resultado)
            {
                return BadRequest(new RespuestaError($"{medida.Mensaje}"));
            }

            if (medida.Dato == null)
            {
                return NotFound(new RespuestaError("El código de la medida no se encontró."));
            }

            return Ok(medida.Dato);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<List<MedidaArticuloDTO>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var medidas = await _medidaArticuloApplication.ObtenerContengaNombreAsync(name);

            if (!medidas.Resultado)
            {
                return BadRequest(new RespuestaError(medidas.Mensaje));
            }

            return Ok(medidas.Dato);
        }

        [HttpGet]
        public async Task<ActionResult<List<MedidaArticuloDTO>>> ObtenerTodoAsync()
        {
            var medidas = await _medidaArticuloApplication.ObtenerTodoAsync();

            if (!medidas.Resultado)
            {
                return BadRequest(new RespuestaError(medidas.Mensaje));
            }

            return Ok(medidas.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> InsertarAsync([FromBody] MedidaArticuloCrearDTO obj)
        {
            var insert = await _medidaArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(new RespuestaError(insert.Mensaje));
            }

            return Ok();
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> ActualizarAsync([FromRoute] int id, [FromBody] MedidaArticuloActualizarDTO obj)
        {
            var medida = await _medidaArticuloApplication.ObtenerAsync(id);

            if (medida.Dato == null)
            {
                return NotFound(new RespuestaError("El código de la medida no se encontró."));
            }

            var update = await _medidaArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(new RespuestaError(update.Mensaje));
            }

            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> EliminarAsync([FromRoute] int id)
        {
            var medida = await _medidaArticuloApplication.ObtenerAsync(id);

            if (medida.Dato == null)
            {
                return NotFound(new RespuestaError("El código de la medida no se encontró."));
            }

            var delete = await _medidaArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(new RespuestaError(delete.Mensaje));
            }

            return Ok();
        }
    }
}
