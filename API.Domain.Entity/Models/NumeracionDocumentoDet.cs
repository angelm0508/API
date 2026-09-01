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

    public virtual ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();

    public virtual ICollection<Cotizacion> Cotizacions { get; set; } = new List<Cotizacion>();

    public virtual ICollection<Entrega> Entregas { get; set; } = new List<Entrega>();

    public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();

    public virtual ICollection<PedidoCompra> PedidoCompras { get; set; } = new List<PedidoCompra>();

    public virtual ICollection<EntregaCompra> EntregaCompras { get; set; } = new List<EntregaCompra>();

    public virtual ICollection<EntradaMercancia> EntradaMercancias { get; set; } = new List<EntradaMercancia>();

    public virtual ICollection<SalidaMercancia> SalidaMercancias { get; set; } = new List<SalidaMercancia>();

    public virtual ICollection<FacturaCompra> FacturaCompras { get; set; } = new List<FacturaCompra>();

    public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();

    public virtual ICollection<SocioNegocio> SocioNegocios { get; set; } = new List<SocioNegocio>();
}
