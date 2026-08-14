using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class GrupoSn
{
    public short Entry { get; set; }

    public string? Nombre { get; set; }

    public string? TipoGrupo { get; set; }

    public string? Bloqueado { get; set; }

    public virtual ICollection<SocioNegocio> SocioNegocios { get; set; } = new List<SocioNegocio>();
}
