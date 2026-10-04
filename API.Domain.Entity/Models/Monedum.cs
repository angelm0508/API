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

    public virtual ICollection<EntregaCompra> EntregaCompras { get; set; } = new List<EntregaCompra>();

    public virtual ICollection<FacturaCompra> FacturaCompras { get; set; } = new List<FacturaCompra>();

    public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();

    public virtual ICollection<NotaDebito> NotaDebitos { get; set; } = new List<NotaDebito>();

    public virtual ICollection<NotaDebitoCompra> NotaDebitoCompras { get; set; } = new List<NotaDebitoCompra>();

    public virtual ICollection<NotaCredito> NotaCreditos { get; set; } = new List<NotaCredito>();

    public virtual ICollection<NotaCreditoCompra> NotaCreditoCompras { get; set; } = new List<NotaCreditoCompra>();
}
