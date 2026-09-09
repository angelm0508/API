namespace API.Domain.Core.Precio
{
    public class PrecioListaInvalidaException : Exception
    {
        public PrecioListaInvalidaException(string mensaje) : base(mensaje) { }

        public static PrecioListaInvalidaException Circular() =>
            new("La cadena de listas de precio es circular.");

        public static PrecioListaInvalidaException DemasiadoProfunda() =>
            new("La cadena de listas de precio es demasiado profunda.");

        public static PrecioListaInvalidaException NoExiste(int entry) =>
            new($"La lista de precio {entry} no está registrada.");
    }
}
