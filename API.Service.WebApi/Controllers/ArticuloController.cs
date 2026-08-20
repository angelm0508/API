using API.Application.DTO;
using API.Application.DTO.articulo.articulo;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Articulo")]
    public class ArticuloController : ControllerBase
    {
        private readonly IArticuloApplication _articuloApplication;

        public ArticuloController(IArticuloApplication articuloApplication)
        {
            _articuloApplication = articuloApplication;
        }

        [HttpGet("{codigo}")]
        public async Task<ActionResult<Respuesta<ArticuloDTO>>> Articulo([FromRoute] string codigo)
        {
            var producto = await _articuloApplication.ObtenerPorCodigoAsync(codigo);

            if (!producto.Resultado)
                return BadRequest(producto);

            if (producto.Dato == null)
            {
                producto.Resultado = false;
                producto.Mensaje = "Código de articulo no encontrado.";
                return NotFound(producto);
            }

            return Ok(producto);
        }

        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<Respuesta<ArticuloDTO>>> ArticuloPorNombre([FromRoute] string nombre)
        {
            var producto = await _articuloApplication.ObtenerPorNombreAsync(nombre);

            if (!producto.Resultado)
                return BadRequest(producto);

            if (producto.Dato == null)
            {
                producto.Resultado = false;
                producto.Mensaje = "Nombre de articulo no encontrado.";
                return NotFound(producto);
            }

            return Ok(producto);
        }

        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<Respuesta<IEnumerable<ArticuloDTO>>>> ArticulosContenganNombre([FromRoute] string nombre)
        {
            var producto = await _articuloApplication.ObtenerContenganNombreAsync(nombre);

            if (!producto.Resultado)
                return BadRequest(producto);

            return Ok(producto);
        }

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<ArticuloDTO>>>> ArticulosContenganCodigo([FromRoute] string codigo)
        {
            var productos = await _articuloApplication.ObtenerContenganCodigoAsync(codigo);

            if (!productos.Resultado)
                return BadRequest(productos);

            return Ok(productos);
        }

        [HttpGet()]
        public async Task<ActionResult<Respuesta<IEnumerable<ArticuloDTO>>>> Obtener()
        {
            var producto = await _articuloApplication.ObtenerAsync();

            if (!producto.Resultado)
                return BadRequest(producto);

            return Ok(producto);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Post([FromBody] ArticuloCrearDTO producto)
        {
            var insertar = await _articuloApplication.InsertarAsync(producto);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Update([FromRoute] string codigo, [FromBody] ArticuloActualizarDTO obj)
        {
            var producto = await _articuloApplication.ObtenerPorCodigoAsync(codigo);

            if (producto.Dato == null)
            {
                producto.Resultado = false;
                producto.Mensaje = "Código de articulo no encontrado.";
                return NotFound(producto);
            }

            var insert = await _articuloApplication.ActualizarAsync(codigo, obj);

            if (!insert.Resultado)
                return BadRequest(insert);

            return Ok(insert);
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Delete([FromRoute] string codigo)
        {
            var producto = await _articuloApplication.ObtenerPorCodigoAsync(codigo);

            if (producto.Dato == null)
            {
                producto.Resultado = false;
                producto.Mensaje = "Código de articulo no encontrado.";
                return NotFound(producto);
            }

            var eliminar = await _articuloApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
