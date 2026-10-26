using API.Application.DTO;
using API.Application.DTO.estadoCuenta;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    /// <summary>
    /// Estado de cuenta y saldos por socio de negocio. Las rutas son absolutas por acción: "Saldos" es POST y no
    /// colisiona con el GET de {codigoSn}.
    /// </summary>
    [Authorize]
    [ApiController]
    public class EstadoCuentaController : ControllerBase
    {
        private const string NoEncontrado = "El socio de negocio no existe.";

        private readonly IEstadoCuentaApplication _application;

        public EstadoCuentaController(IEstadoCuentaApplication application)
        {
            _application = application;
        }

        [RequierePermiso("EstadoCuenta", Accion.Ver)]
        [HttpGet("~/api/EstadoCuenta/{codigoSn}")]
        public async Task<ActionResult<Respuesta<EstadoCuentaDTO>>> Obtener([FromRoute] string codigoSn, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var estado = await _application.ObtenerAsync(codigoSn, desde, hasta);

            if (!estado.Resultado)
                return BadRequest(estado);

            if (estado.Dato == null)
            {
                estado.Resultado = false;
                estado.Mensaje = NoEncontrado;
                return NotFound(estado);
            }

            return Ok(estado);
        }

        [RequierePermiso("EstadoCuenta", Accion.Ver)]
        [HttpPost("~/api/EstadoCuenta/Saldos")]
        public async Task<ActionResult<Respuesta<IEnumerable<SaldoSocioDTO>>>> Saldos([FromBody] List<string>? codigosSn)
        {
            var saldos = await _application.ObtenerSaldosAsync(codigosSn ?? new List<string>());

            if (!saldos.Resultado)
                return BadRequest(saldos);

            return Ok(saldos);
        }
    }
}
