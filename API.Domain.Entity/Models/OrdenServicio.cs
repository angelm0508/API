using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class OrdenServicio
{
    public int Entry { get; set; }

    public int NumDoc { get; set; }

    public int Serie { get; set; }

    public string? Cancelado { get; set; }

    public string? NumManual { get; set; }

    public string? TipoObjeto { get; set; }

    public DateTime? FechaDoc { get; set; }

    public DateTime? FechaIngreso { get; set; }

    public DateTime? FechaPromesa { get; set; }

    public DateTime? FechaEntrega { get; set; }

    public DateTime? FechaCancelado { get; set; }

    public string? CodigoSn { get; set; }

    public string? NombreSn { get; set; }

    public string? Direccion { get; set; }

    public string? MonedaDoc { get; set; }

    public int MotoEntry { get; set; }

    public string? CodMecanico { get; set; }

    public int? KmIngreso { get; set; }

    public string? FallaReportada { get; set; }

    public string? Diagnostico { get; set; }

    public string? Observaciones { get; set; }

    public string EstadoServicio { get; set; } = "R";

    public decimal? TotalBruto { get; set; }

    public decimal? TotalDesc { get; set; }

    public decimal? TotalImp { get; set; }

    public decimal? TotalDoc { get; set; }

    public string? Comentario { get; set; }

    public virtual SocioNegocio? CodigoSnNavigation { get; set; }

    public virtual Monedum? MonedaDocNavigation { get; set; }

    public virtual NumeracionDocumentoDet SerieNavigation { get; set; } = null!;

    public virtual Motocicleta? MotoNavigation { get; set; }

    public virtual Mecanico? MecanicoNavigation { get; set; }
}
