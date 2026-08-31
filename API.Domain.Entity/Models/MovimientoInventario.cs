using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class MovimientoInventario
{
    public int Entry { get; set; }

    public string TipoDoc { get; set; } = null!;

    public int DocEntry { get; set; }

    public int DocLinea { get; set; }

    public string CodArticulo { get; set; } = null!;

    public string CodAlmacen { get; set; } = null!;

    public DateTime Fecha { get; set; }

    public decimal CantidadEntra { get; set; }

    public decimal CantidadSale { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal CostoUnitario { get; set; }

    public decimal ValorMovimiento { get; set; }

    public decimal VariacionPrecio { get; set; }

    public decimal SaldoCantidad { get; set; }

    public decimal SaldoCostoPromedio { get; set; }

    public decimal SaldoValor { get; set; }

    public int? MovReversaDe { get; set; }

    public virtual Articulo CodArticuloNavigation { get; set; } = null!;

    public virtual Almacen CodAlmacenNavigation { get; set; } = null!;

    public virtual MovimientoInventario? MovReversaDeNavigation { get; set; }

    public virtual ICollection<MovimientoInventario> InverseMovReversaDeNavigation { get; set; } = new List<MovimientoInventario>();
}
