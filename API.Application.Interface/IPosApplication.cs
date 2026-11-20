using API.Application.DTO;
using API.Domain.Interface.Pos;

namespace API.Application.Interface
{
    public interface IPosApplication
    {
        Task<Respuesta<PosConfiguracionVista>> ObtenerConfiguracionAsync();

        /// <summary>Guarda la configuración y devuelve la vista resultante (con sus faltantes).</summary>
        Task<Respuesta<PosConfiguracionVista>> GuardarConfiguracionAsync(PosConfiguracionDato dato);

        /// <summary>Datos de carga de la pantalla del POS (configuración, formas de pago, tasa y cliente por defecto).</summary>
        Task<Respuesta<PosContexto>> ObtenerContextoAsync();

        /// <summary>Hasta 10 clientes activos cuyo código o nombre contengan el texto (mínimo 2 caracteres).</summary>
        Task<Respuesta<IReadOnlyList<ClientePos>>> BuscarClientesAsync(string? texto);

        Task<Respuesta<IReadOnlyList<ArticuloPos>>> BuscarArticulosAsync(string? texto, string? codigoCliente);

        Task<Respuesta<ArticuloPos?>> ObtenerArticuloPorCodigoAsync(string codigo, string? codigoCliente);

        Task<Respuesta<IReadOnlyList<ArticuloPos>>> ObtenerRapidosAsync();

        Task<Respuesta<TicketPos>> VenderAsync(VentaPosSolicitud solicitud, string cajero);

        Task<Respuesta<TicketPos>> ObtenerTicketAsync(int facturaEntry);

        Task<Respuesta<IReadOnlyList<VentaPosDelDia>>> ObtenerVentasDelDiaAsync();
    }
}
