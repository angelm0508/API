using API.Application.DTO;
using API.Application.Interface;
using API.Domain.Interface.Pos;

namespace API.Application.Main
{
    public class PosApplication : IPosApplication
    {
        private const string ConsultaCorrecta = "Consulta realizada correctamente.";

        private readonly IPosDomain _domain;

        public PosApplication(IPosDomain domain)
        {
            _domain = domain;
        }

        public Task<Respuesta<PosConfiguracionVista>> ObtenerConfiguracionAsync() =>
            Ejecutar(() => _domain.ObtenerConfiguracionAsync(), ConsultaCorrecta);

        public Task<Respuesta<PosConfiguracionVista>> GuardarConfiguracionAsync(PosConfiguracionDato dato) =>
            Ejecutar(async () =>
            {
                await _domain.GuardarConfiguracionAsync(dato);
                return await _domain.ObtenerConfiguracionAsync();
            }, "Configuración del POS guardada correctamente.");

        public Task<Respuesta<IReadOnlyList<ArticuloPos>>> BuscarArticulosAsync(string? texto, string? codigoCliente) =>
            Ejecutar(() => _domain.BuscarArticulosAsync(texto, codigoCliente), ConsultaCorrecta);

        public Task<Respuesta<ArticuloPos?>> ObtenerArticuloPorCodigoAsync(string codigo, string? codigoCliente) =>
            Ejecutar(() => _domain.ObtenerArticuloPorCodigoAsync(codigo, codigoCliente), ConsultaCorrecta);

        public Task<Respuesta<IReadOnlyList<ArticuloPos>>> ObtenerRapidosAsync() =>
            Ejecutar(() => _domain.ObtenerRapidosAsync(), ConsultaCorrecta);

        public Task<Respuesta<TicketPos>> VenderAsync(VentaPosSolicitud solicitud, string cajero) =>
            Ejecutar(() => _domain.VenderAsync(solicitud, cajero), "Venta registrada correctamente.");

        public Task<Respuesta<TicketPos>> ObtenerTicketAsync(int facturaEntry) =>
            Ejecutar(() => _domain.ObtenerTicketAsync(facturaEntry), ConsultaCorrecta);

        public Task<Respuesta<IReadOnlyList<VentaPosDelDia>>> ObtenerVentasDelDiaAsync() =>
            Ejecutar(() => _domain.ObtenerVentasDelDiaAsync(), ConsultaCorrecta);

        /// <summary>Ejecuta el caso de uso y convierte cualquier excepción de negocio en Resultado = false con su mensaje.</summary>
        private static async Task<Respuesta<T>> Ejecutar<T>(Func<Task<T>> operacion, string mensajeExito)
        {
            var respuesta = new Respuesta<T>();
            try
            {
                respuesta.Dato = await operacion();
                respuesta.Resultado = true;
                respuesta.Mensaje = mensajeExito;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
    }
}
