namespace API.Domain.Interface.Pos
{
    public interface IPosDomain
    {
        Task<PosConfiguracionVista> ObtenerConfiguracionAsync();
        Task GuardarConfiguracionAsync(PosConfiguracionDato dato);
        Task<PosContexto> ObtenerContextoAsync();
        Task<IReadOnlyList<ClientePos>> BuscarClientesAsync(string? texto);
        Task<IReadOnlyList<ArticuloPos>> BuscarArticulosAsync(string? texto, string? codigoCliente);
        Task<ArticuloPos?> ObtenerArticuloPorCodigoAsync(string codigo, string? codigoCliente);
        Task<IReadOnlyList<ArticuloPos>> ObtenerRapidosAsync();
        Task<TicketPos> VenderAsync(VentaPosSolicitud solicitud, string cajero);
        Task<TicketPos> ObtenerTicketAsync(int facturaEntry);
        Task<IReadOnlyList<VentaPosDelDia>> ObtenerVentasDelDiaAsync();
    }
}
