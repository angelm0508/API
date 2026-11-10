using System.Security.Claims;
using API.Application.DTO;
using API.Application.DTO.reporte;
using API.Application.Interface;
using API.Domain.Interface;
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;

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

        private readonly IConfiguration _config;

        public ReporteController(IReporteApplication app, IConfiguration config)
        {
            _app = app;
            _config = config;
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
        public async Task<ActionResult<Respuesta<ResultadoReporte>>> Ejecutar([FromRoute] string codigo, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SolicitudReporte? solicitud)
        {
            if (solicitud is null)
                return BadRequest(new Respuesta<ResultadoReporte> { Mensaje = "La solicitud del reporte es obligatoria." });

            var error = Verificar(_app.ObtenerDefinicion(codigo));
            if (error is not null) return error;

            var respuesta = await _app.EjecutarAsync(codigo, solicitud);
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        [HttpPost("{codigo}/exportar")]
        public async Task<IActionResult> Exportar([FromRoute] string codigo, [FromQuery] string? formato, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ExportarReporteRequest? request)
        {
            if (request is null)
                return BadRequest(new Respuesta<ArchivoReporteDTO> { Mensaje = "La solicitud del reporte es obligatoria." });

            var error = Verificar(_app.ObtenerDefinicion(codigo));
            if (error is not null) return error;

            var empresa = _config["Reportes:Empresa"] ?? "Sistema de Gestión";
            var respuesta = await _app.ExportarAsync(codigo, request, formato ?? "", empresa, User.Identity?.Name ?? "");
            if (!respuesta.Resultado) return BadRequest(respuesta);

            return File(respuesta.Dato.Contenido, respuesta.Dato.ContentType, respuesta.Dato.NombreArchivo);
        }

        [HttpGet("{codigo}/vistas")]
        public async Task<ActionResult<Respuesta<List<ReporteVistaDTO>>>> Vistas([FromRoute] string codigo)
        {
            var error = Verificar(_app.ObtenerDefinicion(codigo));
            if (error is not null) return error;
            if (!TryUsuarioId(out var usuarioId)) return SinUsuario<List<ReporteVistaDTO>>();

            var respuesta = await _app.ObtenerVistasAsync(usuarioId, codigo);
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        [HttpPost("{codigo}/vistas")]
        public async Task<ActionResult<Respuesta<ReporteVistaDTO>>> GuardarVista([FromRoute] string codigo, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] GuardarVistaDTO? vista)
        {
            if (vista is null)
                return BadRequest(new Respuesta<ReporteVistaDTO> { Mensaje = "La vista es obligatoria." });

            var error = Verificar(_app.ObtenerDefinicion(codigo));
            if (error is not null) return error;
            if (!TryUsuarioId(out var usuarioId)) return SinUsuario<ReporteVistaDTO>();

            var respuesta = await _app.GuardarVistaAsync(usuarioId, codigo, vista);
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        [HttpDelete("{codigo}/vistas/{entry:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarVista([FromRoute] string codigo, [FromRoute] int entry)
        {
            var error = Verificar(_app.ObtenerDefinicion(codigo));
            if (error is not null) return error;
            if (!TryUsuarioId(out var usuarioId)) return SinUsuario<bool>();

            var respuesta = await _app.EliminarVistaAsync(usuarioId, codigo, entry);
            if (respuesta.Resultado) return Ok(respuesta);
            return respuesta.Mensaje.EndsWith("no existe.") ? NotFound(respuesta) : BadRequest(respuesta);
        }

        private bool TryUsuarioId(out int usuarioId) =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out usuarioId);

        private ActionResult SinUsuario<T>() =>
            Unauthorized(new Respuesta<T> { Mensaje = "No se pudo identificar al usuario." });

        /// <summary>null si el reporte existe y el usuario puede verlo; si no, el 404 o el 403 que corresponde.</summary>
        private ActionResult? Verificar(Respuesta<ReporteDefinicion> definicion)
        {
            if (!definicion.Resultado) return NotFound(definicion);
            if (!PermisoUsuario.Tiene(User, definicion.Dato.Modulo, Accion.Ver)) return PermisoUsuario.Denegado();
            return null;
        }
    }
}
