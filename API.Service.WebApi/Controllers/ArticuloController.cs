using API.Application.DTO;
using API.Application.DTO.articulo.articulo;
using API.Application.DTO.precio;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
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
        private readonly IPrecioListaApplication _precioListaApplication;

        public ArticuloController(IArticuloApplication articuloApplication, IPrecioListaApplication precioListaApplication)
        {
            _articuloApplication = articuloApplication;
            _precioListaApplication = precioListaApplication;
        }

        [RequierePermiso("Articulos", Accion.Ver)]
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

        [RequierePermiso("Articulos", Accion.Ver)]
        [HttpGet("{codigo}/Precio")]
        public async Task<ActionResult<Respuesta<PrecioResueltoDTO>>> Precio(
            [FromRoute] string codigo, [FromQuery] int? listaPrecio)
        {
            var res = await _precioListaApplication.ResolverAsync(codigo, listaPrecio);
            if (res.Resultado)
                return Ok(res);

            // "El artículo X no existe." viene de ArticuloNoExisteException -> 404.
            // Lista inexistente / ciclo / profundidad -> 400.
            return res.Mensaje != null &&
                   res.Mensaje.Contains("El artículo", StringComparison.OrdinalIgnoreCase)
                ? NotFound(res)
                : BadRequest(res);
        }

        [RequierePermiso("Articulos", Accion.Ver)]
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

        [RequierePermiso("Articulos", Accion.Ver)]
        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<Respuesta<IEnumerable<ArticuloDTO>>>> ArticulosContenganNombre([FromRoute] string nombre)
        {
            var producto = await _articuloApplication.ObtenerContenganNombreAsync(nombre);

            if (!producto.Resultado)
                return BadRequest(producto);

            return Ok(producto);
        }

        [RequierePermiso("Articulos", Accion.Ver)]
        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<ArticuloDTO>>>> ArticulosContenganCodigo([FromRoute] string codigo)
        {
            var productos = await _articuloApplication.ObtenerContenganCodigoAsync(codigo);

            if (!productos.Resultado)
                return BadRequest(productos);

            return Ok(productos);
        }

        [RequierePermiso("Articulos", Accion.Ver)]
        [HttpGet()]
        public async Task<ActionResult<Respuesta<IEnumerable<ArticuloDTO>>>> Obtener()
        {
            var producto = await _articuloApplication.ObtenerAsync();

            if (!producto.Resultado)
                return BadRequest(producto);

            return Ok(producto);
        }

        [RequierePermiso("Articulos", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<string>>> Post([FromBody] ArticuloCrearDTO producto)
        {
            var insertar = await _articuloApplication.InsertarAsync(producto);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("Articulos", Accion.Actualizar)]
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

        [RequierePermiso("Articulos", Accion.Eliminar)]
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
