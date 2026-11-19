namespace API.Domain.Core.Pos
{
    /// <summary>
    /// Detecta errores transitorios de bloqueo de SQL Server (interbloqueo 1205 y tiempo de espera de bloqueo 1222) sin
    /// referenciar el cliente SQL: recorre la cadena de excepciones internas buscando un tipo llamado
    /// <c>SqlException</c> y lee su propiedad pública <c>Number</c> por reflexión.
    /// </summary>
    public static class ErrorSqlTransitorio
    {
        private const int Interbloqueo = 1205;
        private const int TiempoEsperaBloqueo = 1222;
        private const int ProfundidadMaxima = 32;

        public static bool EsInterbloqueo(Exception? excepcion) => Recorrer(excepcion, 0);

        private static bool Recorrer(Exception? ex, int profundidad)
        {
            while (ex is not null && profundidad++ < ProfundidadMaxima)
            {
                if (ex.GetType().Name == "SqlException"
                    && ex.GetType().GetProperty("Number")?.GetValue(ex) is int numero
                    && (numero == Interbloqueo || numero == TiempoEsperaBloqueo))
                    return true;

                if (ex is AggregateException agregada)
                    return agregada.InnerExceptions.Any(i => Recorrer(i, profundidad));

                ex = ex.InnerException;
            }
            return false;
        }
    }
}
