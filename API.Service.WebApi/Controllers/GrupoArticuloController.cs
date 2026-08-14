using API.Application.DTO.articulo.grupo_articulo;
using API.Application.Interface;
using API.Transversal.Common;
using Azure;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace API.Service.WebApi.Controllers
{
    [ApiController]
    [Route("api/GrupoArticulo")]
    public class GrupoArticuloController : ControllerBase
    {
        private readonly IGrupoArticuloApplication _grupoArticuloApplication;

        public GrupoArticuloController(IGrupoArticuloApplication grupoArticuloApplication)
        {
            _grupoArticuloApplication = grupoArticuloApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<GrupoArticuloDTO>> Obtener([FromRoute] int id)
        {
            var grupoArticulo = await _grupoArticuloApplication.ObtenerAsync(id);

            if (!grupoArticulo.Resultado)
            {
                return BadRequest(new RespuestaError($"{grupoArticulo.Mensaje}"));
            }

            if (grupoArticulo.Dato == null)
            {
                return NotFound(new RespuestaError("El código del grupo de articulo no se encontró."));
            }

            return Ok(grupoArticulo.Dato);
        }

        [HttpGet("PorNombre/{name}")]
        public async Task<ActionResult<GrupoArticuloDTO>> ObtenerPorNombre([FromRoute] string name)
        {
            var grupoArticulo = await _grupoArticuloApplication.ObtenerAsync(name);

            if (!grupoArticulo.Resultado)
            {
                return BadRequest(new RespuestaError($"{grupoArticulo.Mensaje}"));
            }

            if (grupoArticulo.Dato == null)
            {
                return NotFound(new RespuestaError("El nombre del grupo de articulo no se encontró."));
            }

            return Ok(grupoArticulo.Dato);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<List<GrupoArticuloDTO>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var grupoArticulos = await _grupoArticuloApplication.ObtenerContengaNombreAsync(name);

            if (!grupoArticulos.Resultado)
            {
                return BadRequest(new RespuestaError(grupoArticulos.Mensaje));
            }

            return Ok(grupoArticulos.Dato);
        }

        [HttpGet]
        public async Task<ActionResult<List<GrupoArticuloDTO>>> ObtenerTodoAsync()
        {
            var grupoArticulos = await _grupoArticuloApplication.ObtenerTodoAsync();

            if (!grupoArticulos.Resultado)
            {
                return BadRequest(new RespuestaError(grupoArticulos.Mensaje));
            }

            return Ok(grupoArticulos.Dato);
        }

        /*
        [HttpGet("allWithPaging")]
        public async Task<ActionResult<PagedList<GrupoArticuloDTO>>> GetAllWithPaging([FromQuery] PaginationParametersDTO paginationParametersDTO)
        {
            var productBrands = await _productBrandApplication.GetAllWithPagingAsync(paginationParametersDTO);

            if (!productBrands.Resultado)
            {
                return BadRequest(new ResponseError(productBrands.Mensaje));
            }

            var metadata = new
            {
                productBrands.Dato.TotalCount,
                productBrands.Dato.PageSize,
                productBrands.Dato.CurrentPage,
                productBrands.Dato.HasNext,
                productBrands.Dato.HasPrevious
            };

            Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(metadata));

            return Ok(productBrands.Dato);
        }
        */

        [HttpPost]
        public async Task<ActionResult> InsertarAsync([FromBody] GrupoArticuloCrearDTO obj)
        {
            var insert = await _grupoArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(new RespuestaError(insert.Mensaje));
            }

            return Ok();
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> ActualizarAsync([FromRoute] int id, [FromBody] GrupoArticuloActualizarDTO obj)
        {
            var grupoArticulo = await _grupoArticuloApplication.ObtenerAsync(id);

            if (grupoArticulo.Dato == null)
            {
                return NotFound(new RespuestaError("El código del grupo de articulo no se encontró."));
            }

            var update = await _grupoArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(new RespuestaError(update.Mensaje));
            }

            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> EliminarAsync([FromRoute] int id)
        {
            var grupoArticulo = await _grupoArticuloApplication.ObtenerAsync(id);

            if (grupoArticulo.Dato == null)
            {
                return NotFound(new RespuestaError("El código del grupo de articulo no se encontró."));
            }

            var delete = await _grupoArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(new RespuestaError(delete.Mensaje));
            }

            return Ok();
        }
    }
}
