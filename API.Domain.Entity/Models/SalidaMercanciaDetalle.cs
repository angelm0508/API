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

    public virtual Almacen? CodAlmacenNavigation { get; set; }

    public virtual Articulo? CodArticuloNavigation { get; set; }
}
