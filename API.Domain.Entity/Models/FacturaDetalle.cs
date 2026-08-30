namespace API.Domain.Entity.Models;

public partial class FacturaDetalle
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    public int? TipoDocDestino { get; set; }

    public int? DocDestinoEntry { get; set; }

    public int? BaseRef { get; set; }

    public int? BaseTipo { get; set; }

    public int? BaseEntry { get; set; }

    public int? BaseLinea { get; set; }

    public string? EstadoLinea { get; set; }

    public string? CodArticulo { get; set; }

    public string? Descripcion { get; set; }

    public decimal? Cantidad { get; set; }

    public decimal? Precio { get; set; }

    public decimal? PrecioBruto { get; set; }

    public decimal? PrctjeDesc { get; set; }

    public string? CodigoImpuesto { get; set; }

    public decimal? Impuesto { get; set; }

    public decimal? TotalLinea { get; set; }

    public string? TipoObjeto { get; set; }

    public string? CodAlmacen { get; set; }

    public virtual Almacen? CodAlmacenNavigation { get; set; }

    public virtual Articulo? CodArticuloNavigation { get; set; }
}
