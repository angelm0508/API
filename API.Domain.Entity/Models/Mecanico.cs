using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Mecanico
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Telefono { get; set; }

    public string Activo { get; set; } = null!;
}
