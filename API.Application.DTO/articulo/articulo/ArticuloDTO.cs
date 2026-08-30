using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.articulo.articulo
{
    public class ArticuloDTO
    {
        public string Codigo { get; set; } = null!;

        public string? Nombre { get; set; }

        public short? CodigoGrupo { get; set; }

        public int? CodigoGrpUnidadMedida { get; set; }

        public int? FabricanteEntry { get; set; }

        public string? Activo { get; set; }

        public string? ArticuloCompra { get; set; }

        public string? ArticuloVenta { get; set; }

        public string? ArticuloInventario { get; set; }

        public decimal? PrecioUnitario { get; set; }

        public decimal? CantDisponible { get; set; }

        public decimal? CantConfirmada { get; set; }

        public decimal? CantPedida { get; set; }

        public string? AlmacenDefecto { get; set; }

        public string? NoApliDesc { get; set; }

        public string? GestNoSerie { get; set; }

        public string? GestLote { get; set; }

        public string? GestPorAlmacen { get; set; }

        public decimal? Minimo { get; set; }

        public decimal? Maximo { get; set; }

        public string? Comentarios { get; set; }

        public int Serie { get; set; }
    }
}
