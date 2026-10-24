using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class PagoRecibidoMedio
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    public string CodFormaPago { get; set; } = null!;

    public decimal Monto { get; set; }

    public string? Referencia { get; set; }

    public string? Banco { get; set; }

    public DateTime? FechaCheque { get; set; }

    public virtual FormaPago? CodFormaPagoNavigation { get; set; }
}
