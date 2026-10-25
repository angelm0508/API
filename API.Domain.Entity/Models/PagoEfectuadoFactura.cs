using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class PagoEfectuadoFactura
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    /// <summary>Entry de FacturaCompra: una factura de compra (DocSubType '--') o una nota de débito de proveedor ('ND').</summary>
    public int FacturaCompraEntry { get; set; }

    public decimal MontoAplicado { get; set; }

    public virtual FacturaCompra? FacturaCompraNavigation { get; set; }
}
