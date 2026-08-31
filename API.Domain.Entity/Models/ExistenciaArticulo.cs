using System;

namespace API.Domain.Entity.Models;

public partial class ExistenciaArticulo
{
    public string CodArticulo { get; set; } = null!;

    public string CodAlmacen { get; set; } = null!;

    public decimal Disponible { get; set; }

    public decimal Comprometido { get; set; }

    public decimal Pedido { get; set; }

    public DateTime FechaActualizacion { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Articulo CodArticuloNavigation { get; set; } = null!;

    public virtual Almacen CodAlmacenNavigation { get; set; } = null!;
}
