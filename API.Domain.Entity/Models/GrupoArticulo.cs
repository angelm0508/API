using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class GrupoArticulo
{
    public short Codigo { get; set; }

    public string? Nombre { get; set; }

    public string? Bloqueado { get; set; }

    public virtual ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();
}
