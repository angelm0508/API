using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class DireccionSocioNegocio
{
    public string Direccion { get; set; } = null!;

    public string CodigoSn { get; set; } = null!;

    public string? Calle { get; set; }

    public string? Bloque { get; set; }

    public string? CodigoPostal { get; set; }

    public string? Pais { get; set; }

    public string? Municipio { get; set; }

    public string? Departamento { get; set; }

    public int? NumLinea { get; set; }

    public string? TipoDireccion { get; set; }

    public virtual SocioNegocio CodigoSnNavigation { get; set; } = null!;

    public virtual Departamento? DepartamentoNavigation { get; set; }

    public virtual Municipio? MunicipioNavigation { get; set; }

    public virtual Pai? PaisNavigation { get; set; }
}
