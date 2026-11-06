using API.Domain.Interface;

namespace API.Domain.Core
{
    /// <summary>Implementación trivial del contexto de autorización de crédito; se registra scoped (una instancia por petición).</summary>
    public sealed class AutorizacionCreditoContexto : IAutorizacionCreditoContexto
    {
        public bool Autorizada { get; set; }
        public string? Usuario { get; set; }
    }
}
