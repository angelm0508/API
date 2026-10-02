using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Plantilla
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;

    public virtual ICollection<PlantillaPermisoDetalle> Detalles { get; set; } = new List<PlantillaPermisoDetalle>();
}
