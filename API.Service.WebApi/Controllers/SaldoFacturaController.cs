using API.Application.DTO;
using API.Application.DTO.pagoRecibido;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    /// <summary>
    /// Saldo y estado de pago de facturas y notas de débito de cliente, para los listados. Es POST porque un listado
    /// grande no cabe en la URL. Las rutas son absolutas por acción para no tocar FacturaController/NotaDebitoController.
    /// </summary>
    [Authorize]
    [ApiController]
    public class SaldoFacturaController : ControllerBase
    {
        private readonly ISaldoFacturaApplication _application;

        public SaldoFacturaController(ISaldoFacturaApplication application)
        {
            _application = application;
        }

        [RequierePermiso("Facturas", Accion.Ver)]
        [HttpPost("api/Factura/Saldos")]
        public Task<ActionResult<Respuesta<IEnumerable<SaldoFacturaDTO>>>> SaldosFacturas([FromBody] List<int>? entries) => Saldos(entries);

        [RequierePermiso("NotasDebito", Accion.Ver)]
        [HttpPost("api/NotaDebito/Saldos")]
        public Task<ActionResult<Respuesta<IEnumerable<SaldoFacturaDTO>>>> SaldosNotasDebito([FromBody] List<int>? entries) => Saldos(entries);

        private async Task<ActionResult<Respuesta<IEnumerable<SaldoFacturaDTO>>>> Saldos(List<int>? entries)
        {
            var saldos = await _application.ObtenerAsync(entries ?? new List<int>());

            if (!saldos.Resultado)
                return BadRequest(saldos);

            return Ok(saldos);
        }
    }
}
