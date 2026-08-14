using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class NumeracionDocumentoDet
{
    public string CodigoObj { get; set; } = null!;

    public int Serie { get; set; }

    public string NombreSerie { get; set; } = null!;

    public int? IniNumero { get; set; }

    public int? SigNumero { get; set; }

    public int? FinNumero { get; set; }

    public string? IniCadena { get; set; }

    public string? FinCadena { get; set; }

    public string? Comentario { get; set; }

    public string? Bloqueado { get; set; }

    public int? CantDigitos { get; set; }

    public string SubTipoDoc { get; set; } = null!;

    public string TipoSerie { get; set; } = null!;

    public string? Manual { get; set; }

    public virtual ICollection<Cotizacion> Cotizacions { get; set; } = new List<Cotizacion>();
}
