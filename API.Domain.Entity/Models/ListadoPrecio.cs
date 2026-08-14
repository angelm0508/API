using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class ListadoPrecio
{
    public int Entry { get; set; }

    public string? Nombre { get; set; }

    public int? Base { get; set; }

    public decimal? Factor { get; set; }

    public short? MetodoRedondeo { get; set; }

    public string? ReglaRedondeo { get; set; }

    public decimal? ExtMonto { get; set; }

    public string? RndFrmtInt { get; set; }

    public string? RndFrmtDec { get; set; }

    public virtual ICollection<SocioNegocio> SocioNegocios { get; set; } = new List<SocioNegocio>();
}
