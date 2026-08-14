using API.Application.DTO.precio.listado_precio;
using API.Application.Interface;
using API.Transversal.Common;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [ApiController]
    [Route("api/ListadoPrecio")]
    public class ListadoPrecioController : ControllerBase
    {
        private readonly IListadoPrecioApplication _listadoPrecioApplication;

        public ListadoPrecioController(IListadoPrecioApplication listadoPrecioApplication)
        {
            _listadoPrecioApplication = listadoPrecioApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ListadoPrecioDTO>> Obtener([FromRoute] int id)
        {
            var listadoPrecio = await _listadoPrecioApplication.ObtenerAsync(id);

            if (!listadoPrecio.Resultado)
            {
                return BadRequest(new RespuestaError($"{listadoPrecio.Mensaje}"));
            }

            if (listadoPrecio.Dato == null)
            {
                return NotFound(new RespuestaError("El código del listado de precio no se encontró."));
            }

            return Ok(listadoPrecio.Dato);
        }

        [HttpGet("PorNombre/{name}")]
        public async Task<ActionResult<ListadoPrecioDTO>> ObtenerPorNombre([FromRoute] string name)
        {
            var listadoPrecio = await _listadoPrecioApplication.ObtenerAsync(name);

            if (!listadoPrecio.Resultado)
            {
                return BadRequest(new RespuestaError($"{listadoPrecio.Mensaje}"));
            }

            if (listadoPrecio.Dato == null)
            {
                return NotFound(new RespuestaError("El nombre del listado de precio no se encontró."));
            }

            return Ok(listadoPrecio.Dato);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<List<ListadoPrecioDTO>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var listadoPrecios = await _listadoPrecioApplication.ObtenerContengaNombreAsync(name);

            if (!listadoPrecios.Resultado)
            {
                return BadRequest(new RespuestaError(listadoPrecios.Mensaje));
            }

            return Ok(listadoPrecios.Dato);
        }

        [HttpGet]
        public async Task<ActionResult<List<ListadoPrecioDTO>>> ObtenerTodoAsync()
        {
            var listadoPrecios = await _listadoPrecioApplication.ObtenerTodoAsync();

            if (!listadoPrecios.Resultado)
            {
                return BadRequest(new RespuestaError(listadoPrecios.Mensaje));
            }

            return Ok(listadoPrecios.Dato);
        }

        [HttpPost]
        public async Task<ActionResult> InsertarAsync([FromBody] ListadoPrecioCrearDTO obj)
        {
            var insert = await _listadoPrecioApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(new RespuestaError(insert.Mensaje));
            }

            return Ok();
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> ActualizarAsync([FromRoute] int id, [FromBody] ListadoPrecioActualizarDTO obj)
        {
            var listadoPrecio = await _listadoPrecioApplication.ObtenerAsync(id);

            if (listadoPrecio.Dato == null)
            {
                return NotFound(new RespuestaError("El código del listado de precio no se encontró."));
            }

            var update = await _listadoPrecioApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(new RespuestaError(update.Mensaje));
            }

            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> EliminarAsync([FromRoute] int id)
        {
            var listadoPrecio = await _listadoPrecioApplication.ObtenerAsync(id);

            if (listadoPrecio.Dato == null)
            {
                return NotFound(new RespuestaError("El código del listado de precio no se encontró."));
            }

            var delete = await _listadoPrecioApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(new RespuestaError(delete.Mensaje));
            }

            return Ok();
        }
    }
}
