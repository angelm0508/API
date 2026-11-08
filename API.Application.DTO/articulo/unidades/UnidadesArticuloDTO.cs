namespace API.Application.DTO.articulo.unidades
{
    public class UnidadArticuloDTO
    {
        public int UomEntry { get; set; }
        public string Codigo { get; set; } = null!;
        public string? Nombre { get; set; }
        /// <summary>Unidades base por 1 unidad de esta (la base tiene 1).</summary>
        public decimal Factor { get; set; }
        public bool EsBase { get; set; }
    }

    public class UnidadesArticuloDTO
    {
        public string CodArticulo { get; set; } = null!;
        public int? GrupoEntry { get; set; }
        /// <summary>La base primero. Vacía si el artículo no tiene grupo de medida.</summary>
        public List<UnidadArticuloDTO> Unidades { get; set; } = new();
    }
}
