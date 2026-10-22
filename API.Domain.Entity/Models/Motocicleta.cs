using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Motocicleta
{
    public int Entry { get; set; }

    public string CodigoSn { get; set; } = null!;

    public string Placa { get; set; } = null!;

    public string? Marca { get; set; }

    public string? Modelo { get; set; }

    public short? Anio { get; set; }

    public string? Color { get; set; }

    public string? Vin { get; set; }

    public string? Comentario { get; set; }

    public string Activo { get; set; } = null!;

    public virtual SocioNegocio? CodigoSnNavigation { get; set; }
}
