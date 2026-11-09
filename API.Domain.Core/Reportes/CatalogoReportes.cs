using API.Domain.Interface.Reportes;

namespace API.Domain.Core.Reportes
{
    /// <summary>Reúne todas las definiciones registradas por DI (una por cada <see cref="IReporte"/>).</summary>
    public sealed class CatalogoReportes : ICatalogoReportes
    {
        private readonly IReadOnlyList<IReporte> _reportes;

        public CatalogoReportes(IEnumerable<IReporte> reportes)
        {
            var lista = reportes
                .OrderBy(r => r.Definicion.Familia, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(r => r.Definicion.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var repetido = lista.GroupBy(r => r.Definicion.Codigo, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (repetido is not null)
                throw new InvalidOperationException($"Código de reporte duplicado: {repetido.Key}");

            _reportes = lista;
        }

        public IReadOnlyList<IReporte> Todos => _reportes;

        public IReporte? Buscar(string codigo) =>
            _reportes.FirstOrDefault(r => string.Equals(r.Definicion.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
    }
}
