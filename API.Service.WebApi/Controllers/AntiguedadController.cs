using API.Application.DTO;
using API.Application.DTO.antiguedad;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    /// <summary>
    /// Antigüedad de saldos por cobrar y por pagar. Las rutas son absolutas por acción y cada acción lleva su propio
    /// permiso (no hay permiso a nivel de clase): quien solo tiene AntiguedadCobrar no puede leer lo de pagar.
    /// </summary>
    [Authorize]
    [ApiController]
    public class AntiguedadController : ControllerBase
    {
        private readonly IAntiguedadApplication _application;

        public AntiguedadController(IAntiguedadApplication application)
        {
            _application = application;
        }

        [RequierePermiso("AntiguedadCobrar", Accion.Ver)]
        [HttpGet("~/api/Antiguedad/Cobrar")]
        public async Task<ActionResult<Respuesta<List<FilaAntiguedadDTO>>>> ObtenerCobrar()
        {
            var respuesta = await _application.ObtenerCobrarAsync();
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        [RequierePermiso("AntiguedadCobrar", Accion.Ver)]
        [HttpGet("~/api/Antiguedad/Cobrar/{codigoSn}")]
        public async Task<ActionResult<Respuesta<List<DocumentoAntiguedadDTO>>>> DetalleCobrar([FromRoute] string codigoSn)
        {
            var respuesta = await _application.ObtenerDetalleCobrarAsync(codigoSn);
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        [RequierePermiso("AntiguedadPagar", Accion.Ver)]
        [HttpGet("~/api/Antiguedad/Pagar")]
        public async Task<ActionResult<Respuesta<List<FilaAntiguedadDTO>>>> ObtenerPagar()
        {
            var respuesta = await _application.ObtenerPagarAsync();
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        [RequierePermiso("AntiguedadPagar", Accion.Ver)]
        [HttpGet("~/api/Antiguedad/Pagar/{codigoSn}")]
        public async Task<ActionResult<Respuesta<List<DocumentoAntiguedadDTO>>>> DetallePagar([FromRoute] string codigoSn)
        {
            var respuesta = await _application.ObtenerDetallePagarAsync(codigoSn);
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }
    }
}
