namespace API.Domain.Entity.Models;

public partial class Impuesto
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public decimal? Tasa { get; set; }
}
