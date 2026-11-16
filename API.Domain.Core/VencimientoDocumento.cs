namespace API.Domain.Core
{
    /// <summary>
    /// Regla del vencimiento al crear una factura o nota de débito (venta o compra): la fecha calculada por la condición de
    /// pago es solo una sugerencia; si el cliente pide otra, se guarda esa (solo el día) siempre que no sea anterior a la
    /// fecha del documento. Compartido por los cuatro dominios de inserción.
    /// </summary>
    public static class VencimientoDocumento
    {
        public const string MensajeAnteriorAlDocumento = "La fecha de vencimiento no puede ser anterior a la fecha del documento.";

        /// <summary>Devuelve la fecha de vencimiento a guardar o lanza si la pedida es anterior (por día) a la del documento.</summary>
        /// <param name="pedido">Fecha enviada por el cliente; null = usar la calculada.</param>
        /// <param name="calculado">Fecha que resultó de la condición de pago.</param>
        /// <param name="fechaDoc">Fecha del documento; sin ella se compara contra hoy (la base que usa el resolutor).</param>
        public static DateTime Aplicar(DateTime? pedido, DateTime calculado, DateTime? fechaDoc)
        {
            if (pedido is null) return calculado;

            var dia = pedido.Value.Date;
            if (dia < (fechaDoc ?? DateTime.Today).Date)
                throw new Exception(MensajeAnteriorAlDocumento);

            return dia;
        }
    }
}
