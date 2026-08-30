using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class SocioNegocio
{
    public string Codigo { get; set; } = null!;

    public string? Nombre { get; set; }

    public string? TipoSn { get; set; }

    public short? GrupoSn { get; set; }

    public string? Cui { get; set; }

    public string? Nit { get; set; }

    public string? PersContacto { get; set; }

    public string? Tel1 { get; set; }

    public string? Tel2 { get; set; }

    public decimal? Descuento { get; set; }

    public int? NumLstPrecio { get; set; }

    public string? Email { get; set; }

    public string? Activo { get; set; }

    public int Serie { get; set; }

    public virtual ICollection<Cotizacion> Cotizacions { get; set; } = new List<Cotizacion>();

    public virtual ICollection<Entrega> Entregas { get; set; } = new List<Entrega>();

    public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();

    public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();

    public virtual ICollection<DireccionSocioNegocio> DireccionSocioNegocios { get; set; } = new List<DireccionSocioNegocio>();

    public virtual GrupoSn? GrupoSnNavigation { get; set; }

    public virtual ListadoPrecio? NumLstPrecioNavigation { get; set; }

    public virtual NumeracionDocumentoDet SerieNavigation { get; set; } = null!;
}
