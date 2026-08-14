using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class NumeracionDocumento
{
    public string CodigoObj { get; set; } = null!;

    public int? SerieDfct { get; set; }

    public string? DocAlias { get; set; }

    public string SubTipoDoc { get; set; } = null!;
}
