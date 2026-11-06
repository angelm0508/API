using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

/// <summary>Registro (sin borrado) de un documento de venta creado por encima del límite de crédito o con saldo vencido, con la autorización de un usuario.</summary>
public partial class AutorizacionCredito
{
    public int Id { get; set; }

    /// <summary>Tipo de objeto del documento autorizado (3 cotización, 4 pedido, 5 entrega, 6 factura, 70 orden de servicio).</summary>
    public int TipoDocumento { get; set; }

    public int EntryDocumento { get; set; }

    public string CodigoSn { get; set; } = null!;

    public string Usuario { get; set; } = null!;

    /// <summary>LIMITE o VENCIDO.</summary>
    public string Motivo { get; set; } = null!;

    public decimal? Limite { get; set; }

    public decimal Saldo { get; set; }

    public decimal Comprometido { get; set; }

    public decimal Vencido { get; set; }

    public decimal Monto { get; set; }

    public decimal Exceso { get; set; }

    public DateTime Fecha { get; set; }

    public virtual SocioNegocio? CodigoSnNavigation { get; set; }
}
