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

    public virtual ICollection<Entrega> Entregas { get; set; } = new List<Entrega>();

    public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();

    public virtual ICollection<PedidoCompra> PedidoCompras { get; set; } = new List<PedidoCompra>();

    public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();
}
