using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Monedum
{
    public string Codigo { get; set; } = null!;

    public string? Nombre { get; set; }

    public string? NombreImpresion { get; set; }

    public string? Centena { get; set; }

    public string? CodigoIso { get; set; }

    public short? TipoReondeo { get; set; }

    public virtual ICollection<Cotizacion> Cotizacions { get; set; } = new List<Cotizacion>();
}
