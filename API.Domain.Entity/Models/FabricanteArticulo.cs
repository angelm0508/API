using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class FabricanteArticulo
{
    public int Entry { get; set; }

    public string Nombre { get; set; } = null!;

    public virtual ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();
}
