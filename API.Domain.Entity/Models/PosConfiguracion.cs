using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

// Configuración única del POS (fila Id = 1): series de factura y pago, almacén, impuesto, cliente por defecto, forma de pago en efectivo y moneda.
public partial class PosConfiguracion
{
    public int Id { get; set; }

    public int? SeriePago { get; set; }

    public int? SerieFactura { get; set; }

    public string? CodAlmacen { get; set; }

    public string? CodigoImpuesto { get; set; }

    public string? ClienteDefecto { get; set; }

    public string? FormaPagoEfectivo { get; set; }

    public string? MonedaDoc { get; set; }
}
