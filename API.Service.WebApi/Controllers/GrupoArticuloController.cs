using API.Application.DTO;
using API.Application.DTO.articulo.grupo_articulo;
using API.Application.Interface;
using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
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
        public async Task<ActionResult<Respuesta<GrupoArticuloDTO>>> Obtener([FromRoute] int id)
        {
            var grupoArticulo = await _grupoArticuloApplication.ObtenerAsync(id);

            if (!grupoArticulo.Resultado)
            {
                return BadRequest(grupoArticulo);
            }

            if (grupoArticulo.Dato == null)
            {
                grupoArticulo.Resultado = false;
                grupoArticulo.Mensaje = "El código del grupo de articulo no se encontró.";
                return NotFound(grupoArticulo);
            }

            return Ok(grupoArticulo);
        }

        [HttpGet("PorNombre/{name}")]
        public async Task<ActionResult<Respuesta<GrupoArticuloDTO>>> ObtenerPorNombre([FromRoute] string name)
        {
            var grupoArticulo = await _grupoArticuloApplication.ObtenerAsync(name);

            if (!grupoArticulo.Resultado)
            {
                return BadRequest(grupoArticulo);
            }

            if (grupoArticulo.Dato == null)
            {
                grupoArticulo.Resultado = false;
                grupoArticulo.Mensaje = "El nombre del grupo de articulo no se encontró.";
                return NotFound(grupoArticulo);
            }

            return Ok(grupoArticulo);
        }

        [HttpGet("Contenga/{name}")]
        public async Task<ActionResult<Respuesta<IEnumerable<GrupoArticuloDTO>>>> ObteneContengaNombreAsync([FromRoute] string name)
        {
            var grupoArticulos = await _grupoArticuloApplication.ObtenerContengaNombreAsync(name);

            if (!grupoArticulos.Resultado)
            {
                return BadRequest(grupoArticulos);
            }

            return Ok(grupoArticulos);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<GrupoArticuloDTO>>>> ObtenerTodoAsync()
        {
            var grupoArticulos = await _grupoArticuloApplication.ObtenerTodoAsync();

            if (!grupoArticulos.Resultado)
            {
                return BadRequest(grupoArticulos);
            }

            return Ok(grupoArticulos);
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
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] GrupoArticuloCrearDTO obj)
        {
            var insert = await _grupoArticuloApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] GrupoArticuloActualizarDTO obj)
        {
            var grupoArticulo = await _grupoArticuloApplication.ObtenerAsync(id);

            if (grupoArticulo.Dato == null)
            {
                grupoArticulo.Resultado = false;
                grupoArticulo.Mensaje = "El código del grupo de articulo no se encontró.";
                return NotFound(grupoArticulo);
            }

            var update = await _grupoArticuloApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var grupoArticulo = await _grupoArticuloApplication.ObtenerAsync(id);

            if (grupoArticulo.Dato == null)
            {
                grupoArticulo.Resultado = false;
                grupoArticulo.Mensaje = "El código del grupo de articulo no se encontró.";
                return NotFound(grupoArticulo);
            }

            var delete = await _grupoArticuloApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
