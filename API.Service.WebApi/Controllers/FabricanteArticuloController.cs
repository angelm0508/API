using API.Application.DTO.articulo.fabricante_articulo;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [ApiController]
    [Route("api/FabricanteArticulo")]
    public class FabricanteArticuloController : ControllerBase
    {
        private readonly IFabricanteArticuloApplication _fabricanteArticuloApplication;

        public FabricanteArticuloController(IFabricanteArticuloApplication fabricanteArticuloApplication)
        {
            _fabricanteArticuloApplication = fabricanteArticuloApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<FabricanteArticuloDTO>> Obtener([FromRoute] int id)
        {
            var fabricante = await _fabricanteArticuloApplication.ObtenerAsync(id);

            if (!fabricante.Resultado)
            {
                return BadRequest(new RespuestaError($"{fabricante.Mensaje}"));
            }

            if (fabricante.Dato == null)
            {
                return NotFound(new RespuestaError("El código del fabricante no se encontró."));
            }

            return Ok(fabricante.Dato);
        }

        [HttpGet("PorNombre/{name}")]
        public async Task<ActionResult<FabricanteArticuloDTO>> ObtenerPorNombre([FromRoute] string name)
        {
            var fabricante = await _fabricanteArticuloApplication.ObtenerAsync(name);

            if (!fabricante.Resultado)
            {
                return BadRequest(new RespuestaError($"{fabricante.Mensaje}"));
            }

            if (fabricante.Dato == null)
            {
                return NotFound(new RespuestaError("El nombre del fabricante no se encontró."));
            }

            return Ok(fabricante.Dato);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<List<FabricanteArticuloDTO>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var fabricantes = await _fabricanteArticuloApplication.ObtenerContengaNombreAsync(name);

            if (!fabricantes.Resultado)
            {
                return BadRequest(new RespuestaError(fabricantes.Mensaje));
            }

            return Ok(fabricantes.Dato);
        }

        [HttpGet]
        public async Task<ActionResult<List<FabricanteArticuloDTO>>> ObtenerTodoAsync()
        {
            var fabricantes = await _fabricanteArticuloApplication.ObtenerTodoAsync();

            if (!fabricantes.Resultado)
            {
                return BadRequest(new RespuestaError(fabricantes.Mensaje));
            }

            return Ok(fabricantes.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> InsertarAsync([FromBody] FabricanteArticuloCrearDTO obj)
        {
            var insert = await _fabricanteArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(new RespuestaError(insert.Mensaje));
            }

            return Ok();
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> ActualizarAsync([FromRoute] int id, [FromBody] FabricanteArticuloActualizarDTO obj)
        {
            var fabricante = await _fabricanteArticuloApplication.ObtenerAsync(id);

            if (fabricante.Dato == null)
            {
                return NotFound(new RespuestaError("El código del fabricante no se encontró."));
            }

            var update = await _fabricanteArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(new RespuestaError(update.Mensaje));
            }

            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> EliminarAsync([FromRoute] int id)
        {
            var fabricante = await _fabricanteArticuloApplication.ObtenerAsync(id);

            if (fabricante.Dato == null)
            {
                return NotFound(new RespuestaError("El código del fabricante no se encontró."));
            }

            var delete = await _fabricanteArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(new RespuestaError(delete.Mensaje));
            }

            return Ok();
        }
    }
}
