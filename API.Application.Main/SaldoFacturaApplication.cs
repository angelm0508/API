using API.Application.DTO;
using API.Application.DTO.pagoRecibido;
using API.Application.Interface;
using API.Domain.Interface;

namespace API.Application.Main
{
    public class SaldoFacturaApplication : ISaldoFacturaApplication
    {
        private readonly ISaldoFacturaService _saldos;

        public SaldoFacturaApplication(ISaldoFacturaService saldos)
        {
            _saldos = saldos;
        }

        #region async methods
        public async Task<Respuesta<IEnumerable<SaldoFacturaDTO>>> ObtenerAsync(IEnumerable<int> entries)
        {
            var respuesta = new Respuesta<IEnumerable<SaldoFacturaDTO>>();
            try
            {
                var saldos = await _saldos.ObtenerAsync(entries ?? Enumerable.Empty<int>());
                respuesta.Dato = saldos.Values.Select(s => new SaldoFacturaDTO
                {
                    FacturaEntry = s.FacturaEntry,
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
