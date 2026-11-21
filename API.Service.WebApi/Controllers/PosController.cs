using System.Security.Claims;
using API.Application.DTO;
using API.Application.Interface;
using API.Domain.Interface;
using API.Domain.Interface.Pos;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;

namespace API.Service.WebApi.Controllers
{
    /// <summary>Punto de venta (facturación rápida): configuración, catálogo, venta, ticket y ventas del día.</summary>
    [Authorize]
    [ApiController]
    [Route("api/Pos")]
    public class PosController : ControllerBase
    {
        /// <summary>Convenio del proyecto: un mensaje que dice que algo «no existe» se responde con 404 (p. ej. «La venta no existe.»).</summary>
        private const string NoExiste = "no existe";

        private readonly IPosApplication _app;
        private readonly IConfiguration _config;

        public PosController(IPosApplication app, IConfiguration config)
        {
            _app = app;
            _config = config;
        }

        private string Empresa => _config["Reportes:Empresa"] ?? "Sistema de Gestión";

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

        /// <summary>
        /// Datos de carga de la pantalla del POS: configuración y faltantes, formas de pago activas, tasa del impuesto y
        /// cliente por defecto. Solo pide Pos.Ver: el cajero no necesita permisos de formas de pago, impuestos ni socios.
        /// </summary>
        [HttpGet("Contexto")]
        [RequierePermiso("Pos", Accion.Ver)]
        public async Task<ActionResult<Respuesta<PosContexto>>> ObtenerContexto()
        {
            var respuesta = await _app.ObtenerContextoAsync();
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        /// <summary>Clientes para «Cambiar cliente»: hasta 10 activos cuyo código, nombre o NIT contengan el texto (mínimo 2 caracteres).</summary>
        [HttpGet("Clientes")]
        [RequierePermiso("Pos", Accion.Ver)]
        public async Task<ActionResult<Respuesta<IReadOnlyList<ClientePos>>>> BuscarClientes([FromQuery] string? texto)
        {
            var respuesta = await _app.BuscarClientesAsync(texto);
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        /// <summary>
        /// Cliente nuevo rápido desde el POS (nombre y NIT; de contado, en la serie automática por defecto de clientes). Pide
        /// Pos.Agregar: el cajero no necesita permisos de Socios de negocio.
        /// </summary>
        [HttpPost("Clientes")]
        [RequierePermiso("Pos", Accion.Agregar)]
        public async Task<ActionResult<Respuesta<ClientePos>>> CrearCliente([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NuevoClientePos? solicitud)
        {
            if (solicitud is null)
                return BadRequest(new Respuesta<ClientePos> { Mensaje = "Los datos del cliente son obligatorios." });

            var respuesta = await _app.CrearClienteAsync(solicitud);
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

        /// <summary>Cobra la venta (factura de contado + cobro en una sola transacción) y devuelve el ticket.</summary>
        [HttpPost("Venta")]
        [RequierePermiso("Pos", Accion.Agregar)]
        public async Task<ActionResult<Respuesta<TicketPos>>> Vender([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VentaPosSolicitud? solicitud)
        {
            if (solicitud is null)
                return BadRequest(new Respuesta<TicketPos> { Mensaje = "La venta es obligatoria." });

            var cajero = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrWhiteSpace(cajero))
                return BadRequest(new Respuesta<TicketPos> { Mensaje = "No se pudo identificar al cajero." });

            var respuesta = await _app.VenderAsync(solicitud, cajero);
            if (!respuesta.Resultado)
                return BadRequest(respuesta);

            respuesta.Dato = respuesta.Dato! with { Empresa = Empresa };
            return Ok(respuesta);
        }

        /// <summary>Ticket (reimpresión) de una venta POS; 404 si no existe o no es venta POS.</summary>
        [HttpGet("Ticket/{facturaEntry}")]
        [RequierePermiso("Pos", Accion.Ver)]
        public async Task<ActionResult<Respuesta<TicketPos>>> ObtenerTicket(int facturaEntry)
        {
            var respuesta = await _app.ObtenerTicketAsync(facturaEntry);
            if (!respuesta.Resultado)
                return respuesta.Mensaje?.Contains(NoExiste, StringComparison.OrdinalIgnoreCase) == true ? NotFound(respuesta) : BadRequest(respuesta);

            respuesta.Dato = respuesta.Dato! with { Empresa = Empresa };
            return Ok(respuesta);
        }

        /// <summary>Ventas POS de hoy, más recientes primero.</summary>
        [HttpGet("VentasDelDia")]
        [RequierePermiso("Pos", Accion.Ver)]
        public async Task<ActionResult<Respuesta<IReadOnlyList<VentaPosDelDia>>>> ObtenerVentasDelDia()
        {
            var respuesta = await _app.ObtenerVentasDelDiaAsync();
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }
    }
}
