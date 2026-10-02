namespace API.Domain.Entity.Models;

public partial class PlantillaPermisoDetalle
{
    public int PlantillaId { get; set; }
    public string CodigoModulo { get; set; } = null!;
    public bool Ver { get; set; }
    public bool Agregar { get; set; }
    public bool Actualizar { get; set; }
    public bool Eliminar { get; set; }

    public virtual Plantilla? PlantillaNavigation { get; set; }
    public virtual Modulo? ModuloNavigation { get; set; }
}
