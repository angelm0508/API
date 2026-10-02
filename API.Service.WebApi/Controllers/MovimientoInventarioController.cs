using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/MovimientoInventario")]
    public class MovimientoInventarioController : ControllerBase
    {
        private readonly IMovimientoInventarioApplication _app;

        public MovimientoInventarioController(IMovimientoInventarioApplication app)
        {
            _app = app;
        }

        [RequierePermiso("Existencias", Accion.Ver)]
        [HttpGet("PorArticulo/{codArticulo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<MovimientoInventarioDTO>>>> ObtenerPorArticulo(
            [FromRoute] string codArticulo, [FromQuery] string? almacen, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var r = await _app.ObtenerPorArticuloAsync(codArticulo, almacen, desde, hasta);
            return r.Resultado ? Ok(r) : BadRequest(r);
        }
    }
}
