using API.Application.DTO;
using API.Application.DTO.articulo.unidades;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    /// <summary>
    /// Unidades de medida que admite un artículo (la base y las alternativas de su grupo). Comparte la base
    /// api/Articulo con ArticuloController, igual que ArticuloAlmacenController.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/Articulo")]
    public class ArticuloUnidadesController : ControllerBase
    {
        private readonly IUnidadArticuloApplication _application;

        public ArticuloUnidadesController(IUnidadArticuloApplication application)
        {
            _application = application;
        }

        [RequierePermiso("Articulos", Accion.Ver)]
        [HttpGet("{codigo}/Unidades")]
        public async Task<ActionResult<Respuesta<UnidadesArticuloDTO>>> Obtener([FromRoute] string codigo)
        {
            var res = await _application.ObtenerAsync(codigo);
            if (res.Resultado)
                return Ok(res);

            // "El artículo X no existe." -> 404; cualquier otro error de datos -> 400.
            return res.Mensaje != null && res.Mensaje.Contains("no existe", StringComparison.OrdinalIgnoreCase)
                && res.Mensaje.StartsWith("El artículo", StringComparison.OrdinalIgnoreCase)
                ? NotFound(res)
                : BadRequest(res);
        }
    }
}
