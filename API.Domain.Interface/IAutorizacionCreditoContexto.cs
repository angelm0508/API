namespace API.Domain.Interface
{
    /// <summary>
    /// Contexto por petición (scoped) que el controlador llena cuando el usuario pide autorizar un exceso de crédito y tiene
    /// el permiso; los dominios de Pedido, Entrega y Factura lo leen al verificar el crédito del documento nuevo.
    /// </summary>
    public interface IAutorizacionCreditoContexto
    {
        /// <summary>Verdadero si el usuario autorizó crear el documento aunque supere el límite o el cliente tenga vencido.</summary>
        bool Autorizada { get; set; }
        /// <summary>Usuario que autoriza (se registra en AutorizacionCredito).</summary>
        string? Usuario { get; set; }
    }
}
