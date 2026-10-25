using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class PagoEfectuado
{
    public int Entry { get; set; }

    public int NumDoc { get; set; }

    public int Serie { get; set; }

    public string? Cancelado { get; set; }

    public string? NumManual { get; set; }

    public string? TipoObjeto { get; set; }

    public DateTime? FechaDoc { get; set; }

    public DateTime? FechaCancelado { get; set; }

    public string? CodigoSn { get; set; }

    public string? NombreSn { get; set; }

    public decimal TotalPago { get; set; }

    public string? Comentario { get; set; }

    public virtual SocioNegocio? CodigoSnNavigation { get; set; }

    public virtual NumeracionDocumentoDet? SerieNavigation { get; set; }

    public virtual ICollection<PagoEfectuadoFactura> Facturas { get; set; } = new List<PagoEfectuadoFactura>();

    public virtual ICollection<PagoEfectuadoMedio> Medios { get; set; } = new List<PagoEfectuadoMedio>();
}
