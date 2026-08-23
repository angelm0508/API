namespace API.Application.DTO.articulo.grupo_unidad_medida_det_articulo
{
    public class GrupoUnidadMedidaDetArticuloActualizarDTO
    {
        public int MedidaEntry { get; set; }
        public decimal? CantAlternativa { get; set; }
        public decimal? CantBase { get; set; }
        public int? PesoFactor { get; set; }
        public int? UdfFactor { get; set; }
        public string? Activo { get; set; }
    }
}
