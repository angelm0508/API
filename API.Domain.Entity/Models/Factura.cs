using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Factura
{
    public int Entry { get; set; }

    public int NumDoc { get; set; }

    public int Serie { get; set; }

    public string? Cancelado { get; set; }

    public string? NumManual { get; set; }

    public string? Imprimido { get; set; }

    public string? EstadoDoc { get; set; }

    public string? EstadoInv { get; set; }

    public string? TipoObjeto { get; set; }

    /// <summary>'--' = factura; 'ND' = nota de débito de cliente (modelo SAP B1: misma tabla, distinto subtipo).</summary>
    public string DocSubType { get; set; } = "--";

    /// <summary>'I' = por artículos (mueve inventario); 'S' = por concepto. La factura normal es siempre 'I'.</summary>
    public string DocType { get; set; } = "I";

    public DateTime? FechaDoc { get; set; }

    public DateTime? FechaEmision { get; set; }

    public DateTime? FechaCancelado { get; set; }

    public string? CodigoSn { get; set; }

    public string? NombreSn { get; set; }

    public string? Direccion { get; set; }

    public string? MonedaDoc { get; set; }

    public int? BaseTipo { get; set; }

    public int? BaseEntry { get; set; }

    public decimal? PrctjeImpuesto { get; set; }

    public decimal? TotalImp { get; set; }

    public decimal? PrctjeDesc { get; set; }

    public decimal? TotalDesc { get; set; }

    public decimal? TotalBruto { get; set; }

    public decimal? TotalDoc { get; set; }

    public string? Comentario { get; set; }

    public virtual SocioNegocio? CodigoSnNavigation { get; set; }

    public virtual Monedum? MonedaDocNavigation { get; set; }

    public virtual NumeracionDocumentoDet SerieNavigation { get; set; } = null!;
}
