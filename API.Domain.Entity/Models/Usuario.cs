using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Usuario
{
    public int Id { get; set; }

    public string? Password { get; set; }

    public int LlaveInterna { get; set; }

    public string Codigo { get; set; } = null!;

    public string? Nombre { get; set; }

    public string? Eliminado { get; set; }

    public string? SuperUsuario { get; set; }

    public string? Email { get; set; }

    public string? Bloqueado { get; set; }

    public string? UltimaContra { get; set; }
}
