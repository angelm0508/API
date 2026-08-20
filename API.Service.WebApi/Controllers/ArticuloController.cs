using API.Application.DTO.articulo.articulo;
using API.Application.Interface;
using API.Transversal.Common;
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
        public async Task<ActionResult<ArticuloDTO>> Articulo([FromRoute] string codigo)
        {
            var producto = await _articuloApplication.ObtenerPorCodigoAsync(codigo);
            
            if (!producto.Resultado)
                return BadRequest(new RespuestaError(producto.Mensaje));

            if (producto.Dato == null)
                return NotFound(new RespuestaError("Código de articulo no encontrado."));

            return Ok(producto.Dato);
        }

        
        [HttpGet("Nombre/{nombre}")]
        public async Task<ActionResult<ArticuloDTO>> ArticuloPorNombre([FromRoute] string nombre)
        {
            var producto = await _articuloApplication.ObtenerPorNombreAsync(nombre);

            if (!producto.Resultado)
                return BadRequest(new RespuestaError(producto.Mensaje));

            if (producto.Dato == null)
                return NotFound(new RespuestaError("Nombre de articulo no encontrado."));

            return Ok(producto.Dato);
        }

        
        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<List<ArticuloDTO>>> ArticulosContenganNombre([FromRoute] string nombre)
        {
            var producto = await _articuloApplication.ObtenerContenganNombreAsync(nombre);

            if (!producto.Resultado)
                return BadRequest(new RespuestaError(producto.Mensaje));

            return Ok(producto.Dato);
        }

        

        [HttpGet("ContengaCodigo/{codigo}")]
        public async Task<ActionResult<List<ArticuloDTO>>> ArticulosContenganCodigo([FromRoute] string codigo)
        {
            var productos = await _articuloApplication.ObtenerContenganCodigoAsync(codigo);

            if (!productos.Resultado)
                return BadRequest(new RespuestaError(productos.Mensaje));

            return Ok(productos.Dato);
        }


        
        //[HttpGet("all")]
        [HttpGet()]
        public async Task<ActionResult<List<ArticuloDTO>>> Obtener()
        {
            var producto = await _articuloApplication.ObtenerAsync();

            if (!producto.Resultado)
                return BadRequest(new RespuestaError(producto.Mensaje));

            return Ok(producto.Dato);
        }
        

        /*
        [HttpGet("allWithPaging")]
        public async Task<ActionResult> GetAllWithPaging([FromQuery] PaginationParametersDTO paginationParametersDTO)
        {
            var productos = await _articuloApplication.GetAllWithPagingAsync(paginationParametersDTO);

            if (!productos.IsSuccess)
            {
                return BadRequest(new RespuestaError(productos.Message));
            }

            var metadata = new
            {
                productos.Data.TotalCount,
                productos.Data.PageSize,
                productos.Data.CurrentPage,
                productos.Data.HasNext,
                productos.Data.HasPrevious
            };

            Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(productos.Data);
        }
        */

    
        [HttpPost]
        public async Task<ActionResult> Post([FromBody] ArticuloCrearDTO producto)
        {
            var insertar = await _articuloApplication.InsertarAsync(producto);

            if (!insertar.Resultado)
                return BadRequest(new RespuestaError(insertar.Mensaje));

            return Ok();
        }

        [HttpPut("{codigo}")]
        public async Task<ActionResult> Update([FromRoute] string codigo, [FromBody] ArticuloActualizarDTO obj)
        {
            var producto = await _articuloApplication.ObtenerPorCodigoAsync(codigo);

            if (producto.Dato == null)
                return NotFound(new RespuestaError("Código de articulo no encontrado."));

            var insert = await _articuloApplication.ActualizarAsync(codigo, obj);

            if (!insert.Resultado)
                return BadRequest(new RespuestaError(insert.Mensaje));

            return Ok();
        }

        [HttpDelete("{codigo}")]
        public async Task<ActionResult> Delete([FromRoute] string codigo)
        {
            var producto = await _articuloApplication.ObtenerPorCodigoAsync(codigo);

            if (producto.Dato == null)
                return NotFound(new RespuestaError("Código de articulo no encontrado."));

            var eliminar = await _articuloApplication.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(new RespuestaError($"{eliminar.Mensaje}"));

            return Ok();
        }
        
    }
}
