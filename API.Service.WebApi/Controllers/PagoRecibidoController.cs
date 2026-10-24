using API.Application.DTO;
using API.Application.DTO.pagoRecibido;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/PagoRecibido")]
    public class PagoRecibidoController : ControllerBase
    {
        private const string NoEncontrado = "El pago recibido no se encontró.";

        private readonly IPagoRecibidoApplication _application;

        public PagoRecibidoController(IPagoRecibidoApplication application)
        {
            _application = application;
        }

        [RequierePermiso("PagosRecibidos", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<PagoRecibidoDTO>>>> ObtenerTodo()
        {
            var pagos = await _application.ObtenerTodoAsync();

            if (!pagos.Resultado)
                return BadRequest(pagos);

            return Ok(pagos);
        }

        [RequierePermiso("PagosRecibidos", Accion.Ver)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<PagoRecibidoDTO>>> Obtener([FromRoute] int id)
        {
            var pago = await _application.ObtenerAsync(id);

            if (!pago.Resultado)
                return BadRequest(pago);

            if (pago.Dato == null)
            {
                pago.Resultado = false;
                pago.Mensaje = NoEncontrado;
                return NotFound(pago);
            }

            return Ok(pago);
        }

        [RequierePermiso("PagosRecibidos", Accion.Ver)]
        [HttpGet("{id:int}/Facturas")]
        public async Task<ActionResult<Respuesta<IEnumerable<PagoRecibidoFacturaDTO>>>> ObtenerFacturas([FromRoute] int id)
        {
            var facturas = await _application.ObtenerFacturasAsync(id);

            if (!facturas.Resultado)
                return BadRequest(facturas);

            return Ok(facturas);
        }

        [RequierePermiso("PagosRecibidos", Accion.Ver)]
        [HttpGet("{id:int}/Medios")]
        public async Task<ActionResult<Respuesta<IEnumerable<PagoRecibidoMedioDTO>>>> ObtenerMedios([FromRoute] int id)
        {
            var medios = await _application.ObtenerMediosAsync(id);

            if (!medios.Resultado)
                return BadRequest(medios);

            return Ok(medios);
        }

        // Solo lo usa el formulario de nuevo pago: exige Agregar.
        [RequierePermiso("PagosRecibidos", Accion.Agregar)]
        [HttpGet("FacturasAbiertas/{codigoSn}")]
        public async Task<ActionResult<Respuesta<IEnumerable<DocumentoPorCobrarDTO>>>> ObtenerDocumentosPorCobrar([FromRoute] string codigoSn)
        {
            var documentos = await _application.ObtenerDocumentosPorCobrarAsync(codigoSn);

            if (!documentos.Resultado)
                return BadRequest(documentos);

            return Ok(documentos);
        }

        [RequierePermiso("PagosRecibidos", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> Crear([FromBody] PagoRecibidoCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        // Cancelar un pago equivale a anularlo: exige Accion.Eliminar (convención: 'cancelar documento -> Eliminar').
        [RequierePermiso("PagosRecibidos", Accion.Eliminar)]
        [HttpPost("{id:int}/Cancelar")]
        public async Task<ActionResult<Respuesta<bool>>> Cancelar([FromRoute] int id)
        {
            var pago = await _application.ObtenerAsync(id);

            if (pago.Dato == null)
            {
                pago.Resultado = false;
                pago.Mensaje = NoEncontrado;
                return NotFound(pago);
            }

            var cancelar = await _application.CancelarAsync(id);

            if (!cancelar.Resultado)
                return BadRequest(cancelar);

            return Ok(cancelar);
        }
    }
}
