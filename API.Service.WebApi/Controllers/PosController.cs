using API.Application.DTO;
using API.Application.Interface;
using API.Domain.Interface;
using API.Domain.Interface.Pos;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace API.Service.WebApi.Controllers
{
    /// <summary>Punto de venta (facturación rápida). Por ahora solo la configuración; la venta y el catálogo llegan en tareas posteriores.</summary>
    [Authorize]
    [ApiController]
    [Route("api/Pos")]
    public class PosController : ControllerBase
    {
        private readonly IPosApplication _app;

        public PosController(IPosApplication app)
        {
            _app = app;
        }

        /// <summary>La leen el cajero (Pos.Ver) y quien administra la configuración (PosConfiguracion.Ver); se evalúa dentro de la acción.</summary>
        [HttpGet("Configuracion")]
        public async Task<ActionResult<Respuesta<PosConfiguracionVista>>> ObtenerConfiguracion()
        {
            if (!PermisoUsuario.Tiene(User, "Pos", Accion.Ver) && !PermisoUsuario.Tiene(User, "PosConfiguracion", Accion.Ver))
                return PermisoUsuario.Denegado();

            var respuesta = await _app.ObtenerConfiguracionAsync();
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        [HttpPut("Configuracion")]
        [RequierePermiso("PosConfiguracion", Accion.Actualizar)]
        public async Task<ActionResult<Respuesta<PosConfiguracionVista>>> GuardarConfiguracion([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PosConfiguracionDato? dato)
        {
            if (dato is null)
                return BadRequest(new Respuesta<PosConfiguracionVista> { Mensaje = "La configuración del POS es obligatoria." });

            var respuesta = await _app.GuardarConfiguracionAsync(dato);
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }
    }
}
