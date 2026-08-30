using API.Application.DTO;
using API.Application.DTO.articulo.fabricante_articulo;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
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
        public async Task<ActionResult<Respuesta<FabricanteArticuloDTO>>> Obtener([FromRoute] int id)
        {
            var fabricante = await _fabricanteArticuloApplication.ObtenerAsync(id);

            if (!fabricante.Resultado)
            {
                return BadRequest(fabricante);
            }

            if (fabricante.Dato == null)
            {
                fabricante.Resultado = false;
                fabricante.Mensaje = "El código del fabricante no se encontró.";
                return NotFound(fabricante);
            }

            return Ok(fabricante);
        }

        [HttpGet("PorNombre/{name}")]
        public async Task<ActionResult<Respuesta<FabricanteArticuloDTO>>> ObtenerPorNombre([FromRoute] string name)
        {
            var fabricante = await _fabricanteArticuloApplication.ObtenerAsync(name);

            if (!fabricante.Resultado)
            {
                return BadRequest(fabricante);
            }

            if (fabricante.Dato == null)
            {
                fabricante.Resultado = false;
                fabricante.Mensaje = "El nombre del fabricante no se encontró.";
                return NotFound(fabricante);
            }

            return Ok(fabricante);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<Respuesta<IEnumerable<FabricanteArticuloDTO>>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var fabricantes = await _fabricanteArticuloApplication.ObtenerContengaNombreAsync(name);

            if (!fabricantes.Resultado)
            {
                return BadRequest(fabricantes);
            }

            return Ok(fabricantes);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<FabricanteArticuloDTO>>>> ObtenerTodoAsync()
        {
            var fabricantes = await _fabricanteArticuloApplication.ObtenerTodoAsync();

            if (!fabricantes.Resultado)
            {
                return BadRequest(fabricantes);
            }

            return Ok(fabricantes);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] FabricanteArticuloCrearDTO obj)
        {
            var insert = await _fabricanteArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] FabricanteArticuloActualizarDTO obj)
        {
            var fabricante = await _fabricanteArticuloApplication.ObtenerAsync(id);

            if (fabricante.Dato == null)
            {
                fabricante.Resultado = false;
                fabricante.Mensaje = "El código del fabricante no se encontró.";
                return NotFound(fabricante);
            }

            var update = await _fabricanteArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var fabricante = await _fabricanteArticuloApplication.ObtenerAsync(id);

            if (fabricante.Dato == null)
            {
                fabricante.Resultado = false;
                fabricante.Mensaje = "El código del fabricante no se encontró.";
                return NotFound(fabricante);
            }

            var delete = await _fabricanteArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
