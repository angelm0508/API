using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Departamento
{
    public string Codigo { get; set; } = null!;

    public string Pais { get; set; } = null!;

    public string? Nombre { get; set; }

    public virtual ICollection<Almacen> Almacens { get; set; } = new List<Almacen>();

    public virtual ICollection<DireccionSocioNegocio> DireccionSocioNegocios { get; set; } = new List<DireccionSocioNegocio>();

    public virtual ICollection<Municipio> Municipios { get; set; } = new List<Municipio>();

    public virtual Pai PaisNavigation { get; set; } = null!;
}
