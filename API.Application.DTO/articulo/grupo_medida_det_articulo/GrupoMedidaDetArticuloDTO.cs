namespace API.Application.DTO.articulo.grupo_medida_det_articulo
{
    public class GrupoMedidaDetArticuloDTO
    {
        public int Codigo { get; set; }
        public int? CodigoGrpMedida { get; set; }
        public int? CodigoMedida { get; set; }
        public decimal? CantidadBase { get; set; }
        public decimal? CantidadEmpaque { get; set; }
        public string? Bloqueado { get; set; }
    }
}
