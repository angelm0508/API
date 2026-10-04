namespace API.Domain.Entity.Models;

public partial class NotaDebitoCompraDetalle
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    public string Descripcion { get; set; } = null!;

    public decimal? Cantidad { get; set; }

    public decimal? Precio { get; set; }

    public decimal? PrecioBruto { get; set; }

    public decimal? PrctjeDesc { get; set; }

    public string CodigoImpuesto { get; set; } = null!;

    public decimal? Impuesto { get; set; }

    public decimal? TotalLinea { get; set; }

    public string? TipoObjeto { get; set; }

    public virtual Impuesto CodigoImpuestoNavigation { get; set; } = null!;
}
