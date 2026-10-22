using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    /// <summary>Estados de una orden de servicio (columna EstadoServicio). Solo avanzan: R &lt; P &lt; T &lt; E.</summary>
    public static class EstadoOrdenServicio
    {
        public const string Recibida = "R";
        public const string EnProceso = "P";
        public const string Terminada = "T";
        public const string Entregada = "E";

        public static bool EsValido(string? estado) => estado is Recibida or EnProceso or Terminada or Entregada;

        public static int Orden(string? estado) => estado switch
        {
            Recibida => 0,
            EnProceso => 1,
            Terminada => 2,
            Entregada => 3,
            _ => -1
        };

        public static string Nombre(string? estado) => estado switch
        {
            Recibida => "Recibida",
            EnProceso => "En proceso",
            Terminada => "Terminada",
            Entregada => "Entregada",
            _ => estado ?? string.Empty
        };
    }

    public interface IOrdenServicioDomain
    {
        #region async methods
        /// <summary>Registra la orden con sus líneas en una transacción: numera, guarda y asienta la salida de los insumos. Devuelve el Entry.</summary>
        Task<int> InsertarAsync(OrdenServicio obj, IEnumerable<OrdenServicioDetalle> lineas);
        /// <summary>Edita la cabecera (en una orden Entregada solo el comentario). Devuelve false si la orden no existe.</summary>
        Task<bool> ActualizarAsync(int id, OrdenServicio obj);
        /// <summary>Avanza el estado (R, P, T, E); nunca retrocede. Devuelve false si la orden no existe.</summary>
        Task<bool> CambiarEstadoAsync(int id, string nuevoEstado);
        /// <summary>Cancela la orden y devuelve al inventario todos sus insumos. Devuelve false si la orden no existe.</summary>
        Task<bool> CancelarAsync(int id);
        Task<OrdenServicio?> ObtenerAsync(int id);
        Task<IQueryable<OrdenServicio>> ObtenerTodoAsync();
        Task<IEnumerable<OrdenServicioDetalle>> ObtenerLineasAsync(int id);
        #endregion
    }
}
