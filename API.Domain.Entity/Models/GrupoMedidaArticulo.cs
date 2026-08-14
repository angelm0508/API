using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class GrupoMedidaArticulo
{
    public int Entry { get; set; }

    public string? Codigo { get; set; }

    public string? Nombre { get; set; }

    public int? BaseMedida { get; set; }

    public string? Bloqueado { get; set; }

    public virtual ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();

    public virtual MedidaArticulo? BaseMedidaNavigation { get; set; }
}
