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
    /// <summary>Punto de venta (facturación rápida). Configuración y catálogo; la venta llega en tareas posteriores.</summary>
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

        /// <summary>Búsqueda por texto (código, código de barras o nombre; mínimo 2 caracteres). <c>cliente</c> define la lista de precios.</summary>
        [HttpGet("Articulos")]
        [RequierePermiso("Pos", Accion.Ver)]
        public async Task<ActionResult<Respuesta<IReadOnlyList<ArticuloPos>>>> BuscarArticulos([FromQuery] string? texto, [FromQuery] string? cliente)
        {
            var respuesta = await _app.BuscarArticulosAsync(texto, cliente);
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        /// <summary>Código exacto (lector de barras): 404 si no existe o no se puede vender.</summary>
        [HttpGet("Articulos/porCodigo/{codigo}")]
        [RequierePermiso("Pos", Accion.Ver)]
        public async Task<ActionResult<Respuesta<ArticuloPos?>>> ObtenerArticuloPorCodigo(string codigo, [FromQuery] string? cliente)
        {
            var respuesta = await _app.ObtenerArticuloPorCodigoAsync(codigo, cliente);
            if (!respuesta.Resultado)
                return BadRequest(respuesta);
            if (respuesta.Dato is null)
                return NotFound(new Respuesta<ArticuloPos?> { Mensaje = $"El artículo {codigo} no existe o no está disponible para la venta." });
            return Ok(respuesta);
        }

        /// <summary>Artículos de la cuadrícula rápida: los marcados «Mostrar en POS» o, si no hay, los más vendidos.</summary>
        [HttpGet("Rapidos")]
        [RequierePermiso("Pos", Accion.Ver)]
        public async Task<ActionResult<Respuesta<IReadOnlyList<ArticuloPos>>>> ObtenerRapidos()
        {
            var respuesta = await _app.ObtenerRapidosAsync();
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }
    }
}
