using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class UnidadMedidaArticulo
{
    public int Entry { get; set; }

    public string Codigo { get; set; } = null!;

    public string? Nombre { get; set; }

    public decimal? Largo { get; set; }

    public decimal? Ancho { get; set; }

    public decimal? Altura { get; set; }

    public decimal? Volumen { get; set; }

    public decimal? Peso { get; set; }

    public string? Bloqueado { get; set; }

    public virtual ICollection<GrupoUnidadMedidaArticulo> GrupoUnidadMedidaArticulos { get; set; } = new List<GrupoUnidadMedidaArticulo>();

    public virtual ICollection<GrupoUnidadMedidaDetArticulo> GrupoUnidadMedidaDetArticulos { get; set; } = new List<GrupoUnidadMedidaDetArticulo>();
}
