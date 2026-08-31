using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Articulo
{
    public string Codigo { get; set; } = null!;

    public string? Nombre { get; set; }

    public short? CodigoGrupo { get; set; }

    public int? CodigoGrpUnidadMedida { get; set; }

    public int? FabricanteEntry { get; set; }

    public string? Activo { get; set; }

    public string? ArticuloCompra { get; set; }

    public string? ArticuloVenta { get; set; }

    public string? ArticuloInventario { get; set; }

    public decimal? PrecioUnitario { get; set; }

    public decimal? CantDisponible { get; set; }

    public decimal? CantConfirmada { get; set; }

    public decimal? CantPedida { get; set; }

    public string? AlmacenDefecto { get; set; }

    public string? NoApliDesc { get; set; }

    public string? GestNoSerie { get; set; }

    public string? GestLote { get; set; }

    public string? GestPorAlmacen { get; set; }

    public decimal? Minimo { get; set; }

    public decimal? Maximo { get; set; }

    public string? Comentarios { get; set; }

    public int Serie { get; set; }

    public string MetodoValuacion { get; set; } = null!;

    public decimal CostoPromedio { get; set; }

    public decimal CostoEstandar { get; set; }

    public decimal ValorInventario { get; set; }

    public virtual Almacen? AlmacenDefectoNavigation { get; set; }

    public virtual ICollection<CotizacionDetalle> CotizacionDetalles { get; set; } = new List<CotizacionDetalle>();

    public virtual ICollection<EntregaDetalle> EntregaDetalles { get; set; } = new List<EntregaDetalle>();

    public virtual ICollection<PedidoDetalle> PedidoDetalles { get; set; } = new List<PedidoDetalle>();

    public virtual ICollection<PedidoCompraDetalle> PedidoCompraDetalles { get; set; } = new List<PedidoCompraDetalle>();

    public virtual ICollection<EntregaCompraDetalle> EntregaCompraDetalles { get; set; } = new List<EntregaCompraDetalle>();

    public virtual ICollection<FacturaCompraDetalle> FacturaCompraDetalles { get; set; } = new List<FacturaCompraDetalle>();

    public virtual ICollection<FacturaDetalle> FacturaDetalles { get; set; } = new List<FacturaDetalle>();

    public virtual ICollection<ExistenciaArticulo> ExistenciaArticulos { get; set; } = new List<ExistenciaArticulo>();

    public virtual ICollection<MovimientoInventario> MovimientoInventarios { get; set; } = new List<MovimientoInventario>();

    public virtual GrupoUnidadMedidaArticulo? CodigoGrpUnidadMedidaNavigation { get; set; }

    public virtual GrupoArticulo? CodigoGrupoNavigation { get; set; }

    public virtual FabricanteArticulo? FabricanteEntryNavigation { get; set; }

    public virtual NumeracionDocumentoDet SerieNavigation { get; set; } = null!;
}
