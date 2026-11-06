using API.Application.DTO;
using API.Application.DTO.creditoCliente;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    /// <summary>
    /// Posición de crédito de los clientes. Las rutas son absolutas por acción: "Posiciones" es POST y no
    /// colisiona con el GET de {codigoSn}.
    /// </summary>
    [Authorize]
    [ApiController]
    public class CreditoClienteController : ControllerBase
    {
        private const string NoEncontrado = "El cliente no existe.";

        private readonly ICreditoClienteApplication _application;

        public CreditoClienteController(ICreditoClienteApplication application)
        {
            _application = application;
        }

        [RequierePermiso("EstadoCuenta", Accion.Ver)]
        [HttpGet("~/api/CreditoCliente/{codigoSn}")]
        public async Task<ActionResult<Respuesta<PosicionCreditoDTO>>> Obtener([FromRoute] string codigoSn)
        {
            var posicion = await _application.ObtenerPosicionAsync(codigoSn);

            if (!posicion.Resultado)
                return BadRequest(posicion);

            if (posicion.Dato == null)
            {
                posicion.Resultado = false;
                posicion.Mensaje = NoEncontrado;
                return NotFound(posicion);
            }

            return Ok(posicion);
        }

        [RequierePermiso("EstadoCuenta", Accion.Ver)]
        [HttpPost("~/api/CreditoCliente/Posiciones")]
        public async Task<ActionResult<Respuesta<IEnumerable<PosicionCreditoDTO>>>> Posiciones([FromBody] List<string>? codigosSn)
        {
            var posiciones = await _application.ObtenerPosicionesAsync(codigosSn ?? new List<string>());

            if (!posiciones.Resultado)
                return BadRequest(posiciones);

            return Ok(posiciones);
        }
    }
}
