using API.Application.DTO;
using API.Application.DTO.pagoEfectuado;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    /// <summary>
    /// Saldo y estado de pago de facturas de compra y notas de débito de proveedor, para los listados. Es POST porque un
    /// listado grande no cabe en la URL. Las rutas son absolutas por acción para no tocar FacturaCompraController/NotaDebitoCompraController.
    /// </summary>
    [Authorize]
    [ApiController]
    public class SaldoFacturaCompraController : ControllerBase
    {
        private readonly ISaldoFacturaCompraApplication _application;

        public SaldoFacturaCompraController(ISaldoFacturaCompraApplication application)
        {
            _application = application;
        }

        [RequierePermiso("FacturasCompra", Accion.Ver)]
        [HttpPost("~/api/FacturaCompra/Saldos")]
        public Task<ActionResult<Respuesta<IEnumerable<SaldoFacturaCompraDTO>>>> SaldosFacturasCompra([FromBody] List<int>? entries) => Saldos(entries);

        [RequierePermiso("NotasDebitoCompra", Accion.Ver)]
        [HttpPost("~/api/NotaDebitoCompra/Saldos")]
        public Task<ActionResult<Respuesta<IEnumerable<SaldoFacturaCompraDTO>>>> SaldosNotasDebitoCompra([FromBody] List<int>? entries) => Saldos(entries);

        private async Task<ActionResult<Respuesta<IEnumerable<SaldoFacturaCompraDTO>>>> Saldos(List<int>? entries)
        {
            var saldos = await _application.ObtenerAsync(entries ?? new List<int>());

            if (!saldos.Resultado)
                return BadRequest(saldos);

            return Ok(saldos);
        }
    }
}
