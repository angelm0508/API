using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Almacen
{
    public string Codigo { get; set; } = null!;

    public string? Nombre { get; set; }

    public string Activo { get; set; } = null!;

    public string? Calle { get; set; }

    public string? CodigoPostal { get; set; }

    public string? Pais { get; set; }

    public string? Municipio { get; set; }

    public string? Departamento { get; set; }

    public string? Bloqueado { get; set; }

    public virtual ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();

    public virtual ICollection<CotizacionDetalle> CotizacionDetalles { get; set; } = new List<CotizacionDetalle>();

    public virtual ICollection<EntregaDetalle> EntregaDetalles { get; set; } = new List<EntregaDetalle>();

    public virtual ICollection<PedidoDetalle> PedidoDetalles { get; set; } = new List<PedidoDetalle>();

    public virtual ICollection<PedidoCompraDetalle> PedidoCompraDetalles { get; set; } = new List<PedidoCompraDetalle>();

    public virtual ICollection<EntregaCompraDetalle> EntregaCompraDetalles { get; set; } = new List<EntregaCompraDetalle>();

    public virtual ICollection<FacturaCompraDetalle> FacturaCompraDetalles { get; set; } = new List<FacturaCompraDetalle>();

    public virtual ICollection<FacturaDetalle> FacturaDetalles { get; set; } = new List<FacturaDetalle>();

    public virtual ICollection<ExistenciaArticulo> ExistenciaArticulos { get; set; } = new List<ExistenciaArticulo>();

    public virtual ICollection<MovimientoInventario> MovimientoInventarios { get; set; } = new List<MovimientoInventario>();

    public virtual Departamento? DepartamentoNavigation { get; set; }

    public virtual Municipio? MunicipioNavigation { get; set; }

    public virtual Pai? PaisNavigation { get; set; }
}
