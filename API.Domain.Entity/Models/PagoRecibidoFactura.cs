using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class PagoRecibidoFactura
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    /// <summary>Entry de Factura: una factura (DocSubType '--') o una nota de débito de cliente ('ND').</summary>
    public int FacturaEntry { get; set; }

    public decimal MontoAplicado { get; set; }

    public virtual Factura? FacturaNavigation { get; set; }
}
