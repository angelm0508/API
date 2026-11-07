using System;

namespace API.Domain.Entity.Models;

// Configuracion de stock por articulo y almacen: minimo, maximo y bloqueo de salidas (sin existencias, que viven en ExistenciaArticulo).
public partial class ArticuloAlmacen
{
    public string CodArticulo { get; set; } = null!;

    public string CodAlmacen { get; set; } = null!;

    public decimal? Minimo { get; set; }

    public decimal? Maximo { get; set; }

    public string Bloqueado { get; set; } = "N";

    public virtual Articulo CodArticuloNavigation { get; set; } = null!;

    public virtual Almacen CodAlmacenNavigation { get; set; } = null!;
}
