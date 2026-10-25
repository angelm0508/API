using API.Application.DTO;
using API.Application.DTO.pagoEfectuado;
using API.Application.Interface;
using API.Domain.Interface;

namespace API.Application.Main
{
    public class SaldoFacturaCompraApplication : ISaldoFacturaCompraApplication
    {
        private readonly ISaldoFacturaCompraService _saldos;

        public SaldoFacturaCompraApplication(ISaldoFacturaCompraService saldos)
        {
            _saldos = saldos;
        }

        #region async methods
        public async Task<Respuesta<IEnumerable<SaldoFacturaCompraDTO>>> ObtenerAsync(IEnumerable<int> entries)
        {
            var respuesta = new Respuesta<IEnumerable<SaldoFacturaCompraDTO>>();
            try
            {
                var saldos = await _saldos.ObtenerAsync(entries ?? Enumerable.Empty<int>());
                respuesta.Dato = saldos.Values.Select(s => new SaldoFacturaCompraDTO
                {
                    FacturaCompraEntry = s.FacturaCompraEntry,
                    Total = s.Total,
                    Acreditado = s.Acreditado,
                    Pagado = s.Pagado,
                    Saldo = s.Saldo,
                    Estado = s.Estado
                }).ToList();
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion
    }
}
