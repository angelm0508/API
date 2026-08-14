using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Pai
{
    public string Codigo { get; set; } = null!;

    public string? Nombre { get; set; }

    public string? Iso2codigo { get; set; }

    public string? Iso3codigo { get; set; }

    public string? Isonumerico { get; set; }

    public virtual ICollection<Almacen> Almacens { get; set; } = new List<Almacen>();

    public virtual ICollection<Departamento> Departamentos { get; set; } = new List<Departamento>();

    public virtual ICollection<DireccionSocioNegocio> DireccionSocioNegocios { get; set; } = new List<DireccionSocioNegocio>();
}
