using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class GrupoMedidaDetArticulo
{
    public int GrpMedidaEntry { get; set; }

    public int MedidaEntry { get; set; }

    public decimal? CantAlternativa { get; set; }

    public decimal? CantBase { get; set; }

    public int NumLinea { get; set; }

    public int? PesoFactor { get; set; }

    public int? UdfFactor { get; set; }

    public string? Activo { get; set; }

    public virtual MedidaArticulo MedidaEntryNavigation { get; set; } = null!;
}
