namespace API.Domain.Entity
{
    /// <summary>
    /// Clave de un encabezado de NumeracionDocumento: "CodigoObj" (sin subtipo: primera fila del
    /// objeto, comportamiento histórico) o "CodigoObj:SubTipoDoc" (fila exacta, p. ej. "6:ND").
    /// El ':' no aparece en ningún CodigoObj y es válido dentro de un segmento de URL.
    /// </summary>
    public static class NumeracionClave
    {
        public const char Separador = ':';

        public static string Componer(string codigoObj, string? subTipoDoc) =>
            string.IsNullOrEmpty(subTipoDoc) ? codigoObj : $"{codigoObj}{Separador}{subTipoDoc}";

        public static (string CodigoObj, string? SubTipoDoc) Separar(string clave)
        {
            var i = clave.IndexOf(Separador);
            if (i < 0)
                return (clave, null);

            var subTipo = clave[(i + 1)..];
            return (clave[..i], subTipo.Length == 0 ? null : subTipo);
        }
    }
}
