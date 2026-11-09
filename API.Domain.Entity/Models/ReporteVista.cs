using System;

namespace API.Domain.Entity.Models;

// Vista guardada de un reporte (filtros, columnas, orden, agrupacion y grafico) de un usuario. Configuracion es JSON.
public partial class ReporteVista
{
    public int Entry { get; set; }

    public int UsuarioId { get; set; }

    public string CodigoReporte { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string Configuracion { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public DateTime FechaActualizacion { get; set; }
}
