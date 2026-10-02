using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Modulo
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Grupo { get; set; } = null!;

    public virtual ICollection<UsuarioPermiso> UsuarioPermisos { get; set; } = new List<UsuarioPermiso>();
    public virtual ICollection<PlantillaPermisoDetalle> PlantillaPermisoDetalles { get; set; } = new List<PlantillaPermisoDetalle>();
}
