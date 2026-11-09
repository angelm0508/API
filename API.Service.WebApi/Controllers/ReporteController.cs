using API.Application.DTO;
using API.Application.DTO.reporte;
using API.Application.Interface;
using API.Domain.Interface;
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    /// <summary>
    /// Catálogo y ejecución de reportes. El permiso depende del reporte (cada familia tiene su módulo), así que no se usa
    /// [RequierePermiso]: cada acción evalúa Accion.Ver sobre el módulo de la definición ANTES de ejecutar nada.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/Reporte")]
    public class ReporteController : ControllerBase
    {
        private readonly IReporteApplication _app;

        public ReporteController(IReporteApplication app)
        {
            _app = app;
        }

        [HttpGet]
        public ActionResult<Respuesta<List<ReporteResumenDTO>>> Catalogo()
        {
            var respuesta = _app.ObtenerCatalogo(modulo => PermisoUsuario.Tiene(User, modulo, Accion.Ver));
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        [HttpGet("{codigo}")]
        public ActionResult<Respuesta<ReporteDefinicion>> Definicion([FromRoute] string codigo)
        {
            var definicion = _app.ObtenerDefinicion(codigo);
            var error = Verificar(definicion);
            return error ?? Ok(definicion);
        }

        [HttpPost("{codigo}/ejecutar")]
        public async Task<ActionResult<Respuesta<ResultadoReporte>>> Ejecutar([FromRoute] string codigo, [FromBody] SolicitudReporte solicitud)
        {
            if (solicitud is null)
                return BadRequest(new Respuesta<ResultadoReporte> { Mensaje = "La solicitud del reporte es obligatoria." });

            var error = Verificar(_app.ObtenerDefinicion(codigo));
            if (error is not null) return error;

            var respuesta = await _app.EjecutarAsync(codigo, solicitud);
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        /// <summary>null si el reporte existe y el usuario puede verlo; si no, el 404 o el 403 que corresponde.</summary>
        private ActionResult? Verificar(Respuesta<ReporteDefinicion> definicion)
        {
            if (!definicion.Resultado) return NotFound(definicion);
            if (!PermisoUsuario.Tiene(User, definicion.Dato.Modulo, Accion.Ver)) return PermisoUsuario.Denegado();
            return null;
        }
    }
}
