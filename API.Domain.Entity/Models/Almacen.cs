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

    public virtual ICollection<PedidoDetalle> PedidoDetalles { get; set; } = new List<PedidoDetalle>();

    public virtual Departamento? DepartamentoNavigation { get; set; }

    public virtual Municipio? MunicipioNavigation { get; set; }

    public virtual Pai? PaisNavigation { get; set; }
}
