using API.Application.DTO.creditoCliente;

namespace API.Application.DTO
{
    /// <summary>
    /// Respuesta de un alta rechazada por el control de crédito: lleva la posición del cliente en <see cref="Credito"/>.
    /// El controlador la devuelve con 409 (Conflict); cualquier otra respuesta fallida sigue siendo 400.
    /// </summary>
    public class RespuestaCredito<T> : Respuesta<T>
    {
        public CreditoExcedidoDTO? Credito { get; set; }
    }
}
