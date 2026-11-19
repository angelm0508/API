using API.Application.DTO;
using API.Domain.Interface.Pos;

namespace API.Application.Interface
{
    public interface IPosApplication
    {
        Task<Respuesta<PosConfiguracionVista>> ObtenerConfiguracionAsync();

        /// <summary>Guarda la configuración y devuelve la vista resultante (con sus faltantes).</summary>
        Task<Respuesta<PosConfiguracionVista>> GuardarConfiguracionAsync(PosConfiguracionDato dato);

        Task<Respuesta<IReadOnlyList<ArticuloPos>>> BuscarArticulosAsync(string? texto, string? codigoCliente);

        Task<Respuesta<ArticuloPos?>> ObtenerArticuloPorCodigoAsync(string codigo, string? codigoCliente);

        Task<Respuesta<IReadOnlyList<ArticuloPos>>> ObtenerRapidosAsync();

        Task<Respuesta<TicketPos>> VenderAsync(VentaPosSolicitud solicitud, string cajero);

        Task<Respuesta<TicketPos>> ObtenerTicketAsync(int facturaEntry);

        Task<Respuesta<IReadOnlyList<VentaPosDelDia>>> ObtenerVentasDelDiaAsync();
    }
}
