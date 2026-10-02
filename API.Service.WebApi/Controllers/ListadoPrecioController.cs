using API.Application.DTO;
using API.Application.DTO.precio.listado_precio;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/ListadoPrecio")]
    public class ListadoPrecioController : ControllerBase
    {
        private readonly IListadoPrecioApplication _listadoPrecioApplication;

        public ListadoPrecioController(IListadoPrecioApplication listadoPrecioApplication)
        {
            _listadoPrecioApplication = listadoPrecioApplication;
        }

        [RequierePermiso("ListadosPrecio", Accion.Ver)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<ListadoPrecioDTO>>> Obtener([FromRoute] int id)
        {
            var listadoPrecio = await _listadoPrecioApplication.ObtenerAsync(id);

            if (!listadoPrecio.Resultado)
            {
                return BadRequest(listadoPrecio);
            }

            if (listadoPrecio.Dato == null)
            {
                listadoPrecio.Resultado = false;
                listadoPrecio.Mensaje = "El código del listado de precio no se encontró.";
                return NotFound(listadoPrecio);
            }

            return Ok(listadoPrecio);
        }

        [RequierePermiso("ListadosPrecio", Accion.Ver)]
        [HttpGet("PorNombre/{name}")]
        public async Task<ActionResult<Respuesta<ListadoPrecioDTO>>> ObtenerPorNombre([FromRoute] string name)
        {
            var listadoPrecio = await _listadoPrecioApplication.ObtenerAsync(name);

            if (!listadoPrecio.Resultado)
            {
                return BadRequest(listadoPrecio);
            }

            if (listadoPrecio.Dato == null)
            {
                listadoPrecio.Resultado = false;
                listadoPrecio.Mensaje = "El nombre del listado de precio no se encontró.";
                return NotFound(listadoPrecio);
            }

            return Ok(listadoPrecio);
        }

        [RequierePermiso("ListadosPrecio", Accion.Ver)]
        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<Respuesta<IEnumerable<ListadoPrecioDTO>>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var listadoPrecios = await _listadoPrecioApplication.ObtenerContengaNombreAsync(name);

            if (!listadoPrecios.Resultado)
            {
                return BadRequest(listadoPrecios);
            }

            return Ok(listadoPrecios);
        }

        [RequierePermiso("ListadosPrecio", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<ListadoPrecioDTO>>>> ObtenerTodoAsync()
        {
            var listadoPrecios = await _listadoPrecioApplication.ObtenerTodoAsync();

            if (!listadoPrecios.Resultado)
            {
                return BadRequest(listadoPrecios);
            }

            return Ok(listadoPrecios);
        }

        [RequierePermiso("ListadosPrecio", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] ListadoPrecioCrearDTO obj)
        {
            var insert = await _listadoPrecioApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [RequierePermiso("ListadosPrecio", Accion.Actualizar)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] ListadoPrecioActualizarDTO obj)
        {
            var listadoPrecio = await _listadoPrecioApplication.ObtenerAsync(id);

            if (listadoPrecio.Dato == null)
            {
                listadoPrecio.Resultado = false;
                listadoPrecio.Mensaje = "El código del listado de precio no se encontró.";
                return NotFound(listadoPrecio);
            }

            var update = await _listadoPrecioApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [RequierePermiso("ListadosPrecio", Accion.Eliminar)]
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var listadoPrecio = await _listadoPrecioApplication.ObtenerAsync(id);

            if (listadoPrecio.Dato == null)
            {
                listadoPrecio.Resultado = false;
                listadoPrecio.Mensaje = "El código del listado de precio no se encontró.";
                return NotFound(listadoPrecio);
            }

            var delete = await _listadoPrecioApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
