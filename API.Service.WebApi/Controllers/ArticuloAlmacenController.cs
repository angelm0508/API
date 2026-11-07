using API.Application.DTO;
using API.Application.DTO.articulo.almacen;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    /// <summary>
    /// Stock mínimo, máximo y bloqueo de un artículo por almacén. Comparte la base api/Articulo con
    /// ArticuloController: «Almacenes/BajoMinimo» y «{codigo}/Almacenes» tienen dos segmentos con un literal
    /// distinto de «Nombre», «ContengaNombre», «ContengaCodigo» y «Precio», así que no colisionan.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/Articulo")]
    public class ArticuloAlmacenController : ControllerBase
    {
        private readonly IArticuloAlmacenApplication _application;

        public ArticuloAlmacenController(IArticuloAlmacenApplication application)
        {
            _application = application;
        }

        [RequierePermiso("Articulos", Accion.Ver)]
        [HttpGet("{codigo}/Almacenes")]
        public async Task<ActionResult<Respuesta<IEnumerable<ArticuloAlmacenDTO>>>> Obtener([FromRoute] string codigo)
        {
            var res = await _application.ObtenerAsync(codigo);
            if (res.Resultado)
                return Ok(res);

            return ArticuloNoExiste(res.Mensaje) ? NotFound(res) : BadRequest(res);
        }

        [RequierePermiso("Articulos", Accion.Actualizar)]
        [HttpPut("{codigo}/Almacenes")]
        public async Task<ActionResult<Respuesta<bool>>> Guardar(
            [FromRoute] string codigo, [FromBody] List<ArticuloAlmacenGuardarDTO>? filas)
        {
            var res = await _application.GuardarAsync(codigo, filas);
            if (res.Resultado)
                return Ok(res);

            return ArticuloNoExiste(res.Mensaje) ? NotFound(res) : BadRequest(res);
        }

        [RequierePermiso("Articulos", Accion.Ver)]
        [HttpGet("Almacenes/BajoMinimo")]
        public async Task<ActionResult<Respuesta<IEnumerable<AlmacenBajoMinimoDTO>>>> BajoMinimo()
        {
            var res = await _application.ObtenerBajoMinimoAsync();
            if (!res.Resultado)
                return BadRequest(res);

            return Ok(res);
        }

        /// <summary>«El artículo X no existe.» (ArticuloNoExisteException) -> 404; cualquier otro error -> 400.</summary>
        private static bool ArticuloNoExiste(string? mensaje) =>
            mensaje != null
            && mensaje.StartsWith("El artículo", StringComparison.OrdinalIgnoreCase)
            && mensaje.Contains("no existe", StringComparison.OrdinalIgnoreCase);
    }
}
