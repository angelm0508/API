using API.Application.DTO;
using API.Application.DTO.pagoEfectuado;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/PagoEfectuado")]
    public class PagoEfectuadoController : ControllerBase
    {
        private const string NoEncontrado = "El pago efectuado no se encontró.";

        private readonly IPagoEfectuadoApplication _application;

        public PagoEfectuadoController(IPagoEfectuadoApplication application)
        {
            _application = application;
        }

        [RequierePermiso("PagosEfectuados", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<PagoEfectuadoDTO>>>> ObtenerTodos()
        {
            var pagos = await _application.ObtenerTodosAsync();

            if (!pagos.Resultado)
                return BadRequest(pagos);

            return Ok(pagos);
        }

        [RequierePermiso("PagosEfectuados", Accion.Ver)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<PagoEfectuadoDTO>>> Obtener([FromRoute] int id)
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

        [RequierePermiso("PagosEfectuados", Accion.Ver)]
        [HttpGet("{id:int}/Facturas")]
        public async Task<ActionResult<Respuesta<IEnumerable<PagoEfectuadoFacturaDTO>>>> ObtenerFacturas([FromRoute] int id)
        {
            var facturas = await _application.ObtenerFacturasAsync(id);

            if (!facturas.Resultado)
                return BadRequest(facturas);

            return Ok(facturas);
        }

        [RequierePermiso("PagosEfectuados", Accion.Ver)]
        [HttpGet("{id:int}/Medios")]
        public async Task<ActionResult<Respuesta<IEnumerable<PagoEfectuadoMedioDTO>>>> ObtenerMedios([FromRoute] int id)
        {
            var medios = await _application.ObtenerMediosAsync(id);

            if (!medios.Resultado)
                return BadRequest(medios);

            return Ok(medios);
        }

        // Solo lo usa el formulario de nuevo pago: exige Agregar.
        [RequierePermiso("PagosEfectuados", Accion.Agregar)]
        [HttpGet("FacturasAbiertas/{codigoSn}")]
        public async Task<ActionResult<Respuesta<IEnumerable<DocumentoPorPagarDTO>>>> ObtenerAbiertos([FromRoute] string codigoSn)
        {
            var documentos = await _application.ObtenerAbiertosAsync(codigoSn);

            if (!documentos.Resultado)
                return BadRequest(documentos);

            return Ok(documentos);
        }

        [RequierePermiso("PagosEfectuados", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> Crear([FromBody] PagoEfectuadoCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        // Cancelar un pago equivale a anularlo: exige Accion.Eliminar (convención: 'cancelar documento -> Eliminar').
        [RequierePermiso("PagosEfectuados", Accion.Eliminar)]
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
