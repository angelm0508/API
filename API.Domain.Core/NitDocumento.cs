namespace API.Domain.Core
{
    /// <summary>
    /// Regla del NIT al crear un documento de compra o venta: es una copia del NIT del socio de negocio que el usuario puede
    /// cambiar (p. ej. a CF u otro NIT) mientras crea; después queda fijo. Se recorta, un valor vacío o de solo espacios se
    /// guarda como NULL y no puede pasar de 15 caracteres (el ancho de la columna). Los dominios de creación lo aplican
    /// <b>antes</b> de numerar, así un rechazo no consume consecutivo.
    /// </summary>
    public static class NitDocumento
    {
        public const int LongitudMaxima = 15;
        public const string MensajeExcedeLongitud = "El NIT no puede tener más de 15 caracteres.";

        /// <summary>Devuelve el NIT a guardar (recortado; nulo si viene vacío) o lanza si excede 15 caracteres.</summary>
        public static string? Normalizar(string? nit)
        {
            var limpio = nit?.Trim();
            if (string.IsNullOrEmpty(limpio))
                return null;

            if (limpio.Length > LongitudMaxima)
                throw new Exception(MensajeExcedeLongitud);

            return limpio;
        }
    }
}
