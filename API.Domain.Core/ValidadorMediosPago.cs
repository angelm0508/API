using API.Domain.Entity.Models;

namespace API.Domain.Core
{
    /// <summary>
    /// Validación de las formas de pago de un pago (cobro o pago a proveedor): compartida para que ambos módulos
    /// apliquen exactamente las mismas reglas y mensajes. Normaliza referencia, banco y fecha de cheque según el TipoDato.
    /// </summary>
    public static class ValidadorMediosPago
    {
        public static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

        public static async Task ValidarAsync<T>(IReadOnlyList<T> medios, Func<string, Task<FormaPago?>> buscarForma) where T : IMedioPago
        {
            if (medios.Count == 0)
                throw new Exception("Indique al menos una forma de pago.");

            foreach (var medio in medios)
            {
                medio.CodFormaPago = (medio.CodFormaPago ?? string.Empty).Trim();
                var forma = await buscarForma(medio.CodFormaPago)
                    ?? throw new Exception($"La forma de pago {medio.CodFormaPago} no existe.");

                if (forma.Activo != "S")
                    throw new Exception($"La forma de pago {forma.Nombre} está inactiva.");

                if (medio.Monto <= 0m)
                    throw new Exception($"El monto de la forma de pago {forma.Nombre} debe ser mayor que 0.");

                if (Redondear(medio.Monto) != medio.Monto)
                    throw new Exception($"El monto de la forma de pago {forma.Nombre} no puede tener más de 2 decimales.");

                medio.Referencia = Limpiar(medio.Referencia);
                medio.Banco = Limpiar(medio.Banco);

                switch (forma.TipoDato)
                {
                    case "C":
                        if (medio.Referencia is null)
                            throw new Exception($"La forma de pago {forma.Nombre} requiere el número de cheque.");
                        if (medio.Banco is null)
                            throw new Exception($"La forma de pago {forma.Nombre} requiere el banco.");
                        break;
                    case "T":
                        if (medio.Referencia is null)
                            throw new Exception($"La forma de pago {forma.Nombre} requiere la referencia de la transferencia.");
                        break;
                    case "J":
                        if (medio.Referencia is null)
                            throw new Exception($"La forma de pago {forma.Nombre} requiere la autorización o los últimos 4 dígitos de la tarjeta.");
                        break;
                    default:        // 'N': sin dato
                        medio.Referencia = null;
                        medio.Banco = null;
                        break;
                }

                if (forma.TipoDato != "C")
                    medio.FechaCheque = null;
            }
        }

        private static string? Limpiar(string? texto)
        {
            var t = texto?.Trim();
            return string.IsNullOrEmpty(t) ? null : t;
        }
    }
}
