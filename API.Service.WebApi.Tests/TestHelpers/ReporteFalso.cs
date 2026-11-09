using API.Domain.Interface.Reportes;

namespace API.Service.WebApi.Tests.TestHelpers
{
    /// <summary>Reporte de prueba: devuelve las filas dadas (respetando el límite) y registra lo que recibió.</summary>
    internal sealed class ReporteFalso : IReporte
    {
        private readonly IEnumerable<FilaReporte> _filas;

        public ReporteFalso(ReporteDefinicion definicion, IEnumerable<FilaReporte> filas)
        {
            Definicion = definicion;
            _filas = filas;
        }

        public ReporteDefinicion Definicion { get; }
        public FiltrosReporte? FiltrosRecibidos { get; private set; }
        public int? LimiteRecibido { get; private set; }

        public Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            FiltrosRecibidos = filtros;
            LimiteRecibido = limite;
            return Task.FromResult<IReadOnlyList<FilaReporte>>(_filas.Take(limite).ToList());
        }

        public static FilaReporte Fila(params (string Campo, object? Valor)[] pares)
        {
            var fila = new FilaReporte();
            foreach (var (campo, valor) in pares) fila[campo] = valor;
            return fila;
        }
    }
}
