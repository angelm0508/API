namespace API.Domain.Interface
{
    /// <summary>Unidad de medida de una línea ya validada. <c>Factor</c> = unidades base por 1 unidad de la línea.</summary>
    public record UnidadResuelta(int? UomEntry, string? Codigo, decimal Factor);

    /// <summary>Una unidad de medida disponible para un artículo (la base del grupo tiene Factor 1).</summary>
    public record UnidadArticulo(int UomEntry, string Codigo, string? Nombre, decimal Factor, bool EsBase);

    /// <summary>
    /// Unidades que admite un artículo: la base (primera) y las alternativas activas de su grupo.
    /// Lista vacía si el artículo no tiene grupo de medida (o usa el grupo Manual).
    /// </summary>
    public record UnidadesArticulo(string CodArticulo, int? GrupoEntry, IReadOnlyList<UnidadArticulo> Unidades);

    /// <summary>
    /// Unidades de medida en las líneas de los documentos, estilo SAP B1. El inventario siempre se maneja en la
    /// unidad base del grupo del artículo; la línea guarda la unidad elegida y su factor de conversión.
    /// </summary>
    public interface IUnidadMedidaService
    {
        /// <summary>
        /// Valida la unidad de una línea y devuelve su factor. Sin <paramref name="uomEntry"/> usa la unidad base del grupo.
        /// Artículo sin grupo (o grupo Manual): sin unidad y factor 1.
        /// </summary>
        Task<UnidadResuelta> ResolverAsync(string codArticulo, int? uomEntry);

        Task<UnidadesArticulo> ObtenerUnidadesAsync(string codArticulo);

        /// <summary>
        /// Rechaza cambiar el grupo de unidad de medida de un artículo si ya tiene movimientos de inventario o líneas de documento.
        /// </summary>
        Task ValidarCambioGrupoAsync(string codArticulo, int? nuevoGrupoEntry);
    }
}
