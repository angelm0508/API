using System.Globalization;
using API.Domain.Interface.Reportes;

namespace API.Infraestructure.Exportacion
{
    /// <summary>Formato de pantalla de los valores (PDF): fechas yyyy-MM-dd, dinero #,##0.00, cantidades hasta 6 decimales, enteros sin decimales.</summary>
    internal static class FormatoValores
    {
        public static string Texto(ColumnaReporte columna, object? valor)
        {
            if (valor is null) return "";
            if (valor is DateTime fecha) return fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (ComparadorNumerico.EsNumero(valor))
            {
                var n = Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
                return columna.Tipo switch
                {
                    TipoColumna.Dinero => n.ToString("#,##0.00", CultureInfo.InvariantCulture),
                    TipoColumna.Entero => n.ToString("0", CultureInfo.InvariantCulture),
                    _ => n.ToString("#,##0.######", CultureInfo.InvariantCulture)
                };
            }
            return valor.ToString() ?? "";
        }
    }
}
