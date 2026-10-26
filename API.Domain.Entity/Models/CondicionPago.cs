using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class CondicionPago
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    /// <summary>Días de crédito (0 a 365); el vencimiento de un documento nuevo es su fecha más estos días.</summary>
    public int Dias { get; set; }

    public string Activo { get; set; } = "S";
}
