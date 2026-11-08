using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class SalidaMercanciaDetalle
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    public string? CodArticulo { get; set; }

    public string? Descripcion { get; set; }

    public decimal? Cantidad { get; set; }

    public decimal? CostoUnitario { get; set; }

    public decimal? TotalLinea { get; set; }

    public string? CodAlmacen { get; set; }

    public int? UomEntry { get; set; }

    public decimal FactorUom { get; set; } = 1m;

    public decimal? CantidadBase { get; set; }

    public virtual Almacen? CodAlmacenNavigation { get; set; }

    public virtual Articulo? CodArticuloNavigation { get; set; }

    public virtual UnidadMedidaArticulo? UomEntryNavigation { get; set; }
}
