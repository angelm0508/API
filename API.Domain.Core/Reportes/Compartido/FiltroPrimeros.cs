using API.Domain.Interface.Reportes;

namespace API.Domain.Core.Reportes.Compartido
{
    /// <summary>Lectura del filtro «Mostrar los primeros N» de los reportes tipo ranking (Ruling 11).</summary>
    public static class FiltroPrimeros
    {
        public const string Campo = "primeros";

        /// <summary>Devuelve N si el filtro viene con un entero mayor que cero; null si está vacío (todos); si no, lanza.</summary>
        public static int? Leer(FiltrosReporte filtros)
        {
            if (filtros.Texto(Campo) is null) return null;
            var n = filtros.Numero(Campo);
            if (n is not { } valor || valor <= 0m || valor != decimal.Truncate(valor) || valor > int.MaxValue)
                throw new ReporteInvalidoException("El filtro 'Mostrar los primeros' debe ser un entero mayor que cero.");
            return (int)valor;
        }
    }
}
