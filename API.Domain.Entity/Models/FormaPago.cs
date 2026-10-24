using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class FormaPago
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    /// <summary>N = sin dato, C = cheque, T = transferencia, J = tarjeta.</summary>
    public string TipoDato { get; set; } = "N";

    public string Activo { get; set; } = "S";
}
