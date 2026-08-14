using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Municipio
{
    public string Codigo { get; set; } = null!;

    public string Departamento { get; set; } = null!;

    public string Pais { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public virtual ICollection<Almacen> Almacens { get; set; } = new List<Almacen>();

    public virtual Departamento DepartamentoNavigation { get; set; } = null!;

    public virtual ICollection<DireccionSocioNegocio> DireccionSocioNegocios { get; set; } = new List<DireccionSocioNegocio>();
}
