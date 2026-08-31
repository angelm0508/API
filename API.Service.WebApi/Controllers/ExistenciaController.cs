using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Existencia")]
    public class ExistenciaController : ControllerBase
    {
        private readonly IExistenciaApplication _app;

        public ExistenciaController(IExistenciaApplication app)
        {
            _app = app;
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<ExistenciaArticuloDTO>>>> ObtenerTodo([FromQuery] string? articulo, [FromQuery] string? almacen)
        {
            var r = await _app.ObtenerTodoAsync(articulo, almacen);
            return r.Resultado ? Ok(r) : BadRequest(r);
        }

        [HttpGet("{codArticulo}/{codAlmacen}")]
        public async Task<ActionResult<Respuesta<ExistenciaArticuloDTO>>> Obtener([FromRoute] string codArticulo, [FromRoute] string codAlmacen)
        {
            var r = await _app.ObtenerAsync(codArticulo, codAlmacen);
            return r.Resultado ? Ok(r) : BadRequest(r);
        }

        [HttpGet("PorArticulo/{codArticulo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<ExistenciaArticuloDTO>>>> ObtenerPorArticulo([FromRoute] string codArticulo)
        {
            var r = await _app.ObtenerPorArticuloAsync(codArticulo);
            return r.Resultado ? Ok(r) : BadRequest(r);
        }
    }
}
